using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using CardDisputes.Domain;
using CardDisputes.Infrastructure.Entities;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace CardDisputes.Infrastructure;

public sealed record CreateDisputeCommand(
    string? CardTxnRef,
    string? Merchant,
    long AmountMinor,
    string? Currency,
    string? Reason);

public sealed record CreateDisputeResult(int StatusCode, string Body, Guid Id);

public sealed record DisputeView(
    Guid Id,
    string CardTxnRef,
    string Merchant,
    long AmountMinor,
    string Currency,
    string Reason,
    string Status,
    DateTimeOffset OpenedAt,
    DateOnly ResponseDeadline,
    long Version);

public sealed class DisputeService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly DisputesDbContext _db;
    private readonly IdempotencyStore _idempotencyStore;

    public DisputeService(DisputesDbContext db, IdempotencyStore idempotencyStore)
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(idempotencyStore);
        _db = db;
        _idempotencyStore = idempotencyStore;
    }

    public async Task<CreateDisputeResult> CreateAsync(
        CreateDisputeCommand command,
        string clientId,
        string? idempotencyKey,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (string.IsNullOrWhiteSpace(clientId))
        {
            throw new RequestValidationException("A client id is required.");
        }

        var key = idempotencyKey?.Trim();
        if (string.IsNullOrEmpty(key) || key.Length > 64)
        {
            throw new RequestValidationException(
                "The Idempotency-Key header is required and must be 1 to 64 characters.");
        }

        var reason = ParseReason(command.Reason);
        var cardTxnRef = RequireText(command.CardTxnRef, "cardTxnRef");
        var merchant = RequireText(command.Merchant, "merchant");

        Money money;
        try
        {
            money = new Money(command.AmountMinor, command.Currency ?? string.Empty);
        }
        catch (ArgumentException exception)
        {
            throw new RequestValidationException(exception.Message);
        }

        var requestHash = Hash(command);
        var now = DateTimeOffset.UtcNow;

        await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);

        var existing = await _idempotencyStore.FindAsync(clientId, key, cancellationToken);
        if (existing is not null)
        {
            if (!string.Equals(existing.RequestHash, requestHash, StringComparison.Ordinal))
            {
                throw new IdempotencyConflictException(
                    "The idempotency key was used before with a different request body.");
            }

            await transaction.CommitAsync(cancellationToken);
            return new CreateDisputeResult(existing.StatusCode, existing.ResponseBody, ReadId(existing.ResponseBody));
        }

        var row = new DisputeRow
        {
            Id = Guid.NewGuid(),
            CardTxnRef = cardTxnRef,
            Merchant = merchant,
            AmountMinor = money.Amount,
            Currency = money.Currency,
            ReasonCode = ToDb(reason),
            Status = ToDb(DisputeStatus.Opened),
            OpenedAt = now,
            RespondBy = ResponseDeadline.For(reason, now),
            CreatedBy = clientId
        };

        _db.Disputes.Add(row);
        _db.DisputeEvents.Add(new DisputeEvent
        {
            Id = Guid.NewGuid(),
            DisputeId = row.Id,
            FromStatus = null,
            ToStatus = row.Status,
            ClientId = clientId,
            OccurredAt = now
        });

        await _db.SaveChangesAsync(cancellationToken);
        await _db.Entry(row).ReloadAsync(cancellationToken);

        var body = JsonSerializer.Serialize(Map(row), JsonOptions);
        _idempotencyStore.Add(clientId, key, requestHash, StatusCodes.Created, body, now);

        try
        {
            await _db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (IsUniqueViolation(exception))
        {
            await transaction.RollbackAsync(cancellationToken);
            _db.ChangeTracker.Clear();

            var raced = await _idempotencyStore.FindAsync(clientId, key, cancellationToken);
            if (raced is null)
            {
                throw;
            }

            if (!string.Equals(raced.RequestHash, requestHash, StringComparison.Ordinal))
            {
                throw new IdempotencyConflictException(
                    "The idempotency key was used before with a different request body.");
            }

            return new CreateDisputeResult(raced.StatusCode, raced.ResponseBody, ReadId(raced.ResponseBody));
        }

        return new CreateDisputeResult(StatusCodes.Created, body, row.Id);
    }

    public async Task<DisputeView> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        var row = await _db.Disputes
            .AsNoTracking()
            .SingleOrDefaultAsync(dispute => dispute.Id == id, cancellationToken);

        if (row is null)
        {
            throw new DisputeNotFoundException(id);
        }

        return Map(row);
    }

    public async Task<DisputeView> SubmitAsync(Guid id, string clientId, CancellationToken cancellationToken)
    {
        await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);

        var row = await FindTrackedAsync(id, cancellationToken);
        var evidenceCount = await _db.EvidenceFiles
            .CountAsync(file => file.DisputeId == id, cancellationToken);

        var current = ParseStatus(row.Status);
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var transition = DisputeStateMachine.Next(
            current,
            DisputeAction.Submit,
            evidenceCount > 0,
            today,
            row.RespondBy);

        if (transition.Status is not { } next)
        {
            throw new IllegalTransitionException(
                current,
                DisputeStatus.EvidenceSubmitted,
                transition.Rejection ?? TransitionRejection.IllegalTransition);
        }

        await ApplyAsync(row, next, clientId, cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return Map(row);
    }

    public async Task<DisputeView> DecideAsync(
        Guid id,
        string? outcome,
        long version,
        string clientId,
        CancellationToken cancellationToken)
    {
        var target = ParseOutcome(outcome);

        await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);

        var row = await FindTrackedAsync(id, cancellationToken);
        EnsureVersion(row, version);

        var current = ParseStatus(row.Status);
        if (!DisputeStateMachine.CanTransition(current, target))
        {
            throw new IllegalTransitionException(current, target, TransitionRejection.IllegalTransition);
        }

        await ApplyAsync(row, target, clientId, cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return Map(row);
    }

    public async Task<DisputeView> WithdrawAsync(
        Guid id,
        long version,
        string clientId,
        CancellationToken cancellationToken)
    {
        await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);

        var row = await FindTrackedAsync(id, cancellationToken);
        EnsureVersion(row, version);

        var current = ParseStatus(row.Status);
        if (!DisputeStateMachine.CanTransition(current, DisputeStatus.Withdrawn))
        {
            throw new IllegalTransitionException(current, DisputeStatus.Withdrawn, TransitionRejection.IllegalTransition);
        }

        await ApplyAsync(row, DisputeStatus.Withdrawn, clientId, cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return Map(row);
    }

    private async Task<DisputeRow> FindTrackedAsync(Guid id, CancellationToken cancellationToken)
    {
        var row = await _db.Disputes.SingleOrDefaultAsync(dispute => dispute.Id == id, cancellationToken);
        if (row is null)
        {
            throw new DisputeNotFoundException(id);
        }

        return row;
    }

    private async Task ApplyAsync(
        DisputeRow row,
        DisputeStatus next,
        string clientId,
        CancellationToken cancellationToken)
    {
        var from = row.Status;
        row.Status = ToDb(next);

        _db.DisputeEvents.Add(new DisputeEvent
        {
            Id = Guid.NewGuid(),
            DisputeId = row.Id,
            FromStatus = from,
            ToStatus = row.Status,
            ClientId = clientId,
            OccurredAt = DateTimeOffset.UtcNow
        });

        try
        {
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException exception)
        {
            throw new ConcurrencyConflictException(
                "The dispute was changed by another request. Reload it and try again.",
                exception);
        }

        await _db.Entry(row).ReloadAsync(cancellationToken);
    }

    private static void EnsureVersion(DisputeRow row, long version)
    {
        if ((long)row.Version != version)
        {
            throw new ConcurrencyConflictException(
                "The supplied version does not match the current dispute version.");
        }
    }

    private static string RequireText(string? value, string field)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new RequestValidationException($"{field} is required.");
        }

        return value;
    }

    private static ReasonCode ParseReason(string? reason) => reason switch
    {
        "FRAUD_CARD_ABSENT" => ReasonCode.FraudCardAbsent,
        "GOODS_NOT_RECEIVED" => ReasonCode.GoodsNotReceived,
        "DUPLICATE_CHARGE" => ReasonCode.DuplicateCharge,
        "CANCELLED_RECURRING" => ReasonCode.CancelledRecurring,
        "CREDIT_NOT_PROCESSED" => ReasonCode.CreditNotProcessed,
        _ => throw new RequestValidationException("reason is not a known reason code.")
    };

    private static DisputeStatus ParseOutcome(string? outcome) => outcome switch
    {
        "WON" => DisputeStatus.Won,
        "LOST" => DisputeStatus.Lost,
        _ => throw new RequestValidationException("outcome must be WON or LOST.")
    };

    private static DisputeStatus ParseStatus(string status) => status switch
    {
        "OPENED" => DisputeStatus.Opened,
        "EVIDENCE_SUBMITTED" => DisputeStatus.EvidenceSubmitted,
        "WON" => DisputeStatus.Won,
        "LOST" => DisputeStatus.Lost,
        "WITHDRAWN" => DisputeStatus.Withdrawn,
        _ => throw new InvalidOperationException($"Stored dispute status {status} is not known.")
    };

    private static string ToDb(ReasonCode reason) => reason switch
    {
        ReasonCode.FraudCardAbsent => "FRAUD_CARD_ABSENT",
        ReasonCode.GoodsNotReceived => "GOODS_NOT_RECEIVED",
        ReasonCode.DuplicateCharge => "DUPLICATE_CHARGE",
        ReasonCode.CancelledRecurring => "CANCELLED_RECURRING",
        ReasonCode.CreditNotProcessed => "CREDIT_NOT_PROCESSED",
        _ => throw new ArgumentOutOfRangeException(nameof(reason), reason, "Unknown reason code.")
    };

    private static string ToDb(DisputeStatus status) => status switch
    {
        DisputeStatus.Opened => "OPENED",
        DisputeStatus.EvidenceSubmitted => "EVIDENCE_SUBMITTED",
        DisputeStatus.Won => "WON",
        DisputeStatus.Lost => "LOST",
        DisputeStatus.Withdrawn => "WITHDRAWN",
        _ => throw new ArgumentOutOfRangeException(nameof(status), status, "Unknown dispute status.")
    };

    private static DisputeView Map(DisputeRow row) => new(
        row.Id,
        row.CardTxnRef,
        row.Merchant,
        row.AmountMinor,
        row.Currency,
        row.ReasonCode,
        row.Status,
        row.OpenedAt,
        row.RespondBy,
        row.Version);

    private static string Hash(CreateDisputeCommand command)
    {
        var canonical = JsonSerializer.Serialize(
            new
            {
                command.CardTxnRef,
                command.Merchant,
                command.AmountMinor,
                command.Currency,
                command.Reason
            },
            JsonOptions);

        var digest = SHA256.HashData(Encoding.UTF8.GetBytes(canonical));
        return Convert.ToHexString(digest);
    }

    private static Guid ReadId(string responseBody)
    {
        using var document = JsonDocument.Parse(responseBody);
        return document.RootElement.GetProperty("id").GetGuid();
    }

    private static bool IsUniqueViolation(DbUpdateException exception) =>
        exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation };

    private static class StatusCodes
    {
        public const int Created = 201;
    }
}

public sealed class RequestValidationException : Exception
{
    public RequestValidationException(string message)
        : base(message)
    {
    }
}

public sealed class DisputeNotFoundException : Exception
{
    public DisputeNotFoundException(Guid id)
        : base(FormattableString.Invariant($"Dispute {id} was not found."))
    {
        DisputeId = id;
    }

    public Guid DisputeId { get; }
}

public sealed class IllegalTransitionException : Exception
{
    public IllegalTransitionException(
        DisputeStatus from,
        DisputeStatus to,
        TransitionRejection rejection)
        : base(FormattableString.Invariant(
            $"Transition from {from} to {to} is not allowed ({rejection})."))
    {
        From = from;
        To = to;
        Rejection = rejection;
    }

    public DisputeStatus From { get; }

    public DisputeStatus To { get; }

    public TransitionRejection Rejection { get; }
}

public sealed class ConcurrencyConflictException : Exception
{
    public ConcurrencyConflictException(string message)
        : base(message)
    {
    }

    public ConcurrencyConflictException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}

public sealed class IdempotencyConflictException : Exception
{
    public IdempotencyConflictException(string message)
        : base(message)
    {
    }
}
