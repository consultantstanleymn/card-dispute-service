using System.Text;
using CardDisputes.Infrastructure.Entities;
using Microsoft.EntityFrameworkCore;

namespace CardDisputes.Infrastructure;

public sealed record DisputeListItem(
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

public sealed record DisputePage(IReadOnlyList<DisputeListItem> Items, string? NextCursor);

public sealed class DisputeListRow
{
    public Guid Id { get; set; }

    public string CardTxnRef { get; set; } = string.Empty;

    public string Merchant { get; set; } = string.Empty;

    public long AmountMinor { get; set; }

    public string Currency { get; set; } = string.Empty;

    public string ReasonCode { get; set; } = string.Empty;

    public string Status { get; set; } = string.Empty;

    public DateTimeOffset OpenedAt { get; set; }

    public DateOnly RespondBy { get; set; }

    public long Version { get; set; }
}

public sealed class DisputeQueries
{
    public const int DefaultLimit = 25;
    public const int MaxLimit = 100;

    private readonly DisputesDbContext _db;

    public DisputeQueries(DisputesDbContext db)
    {
        ArgumentNullException.ThrowIfNull(db);
        _db = db;
    }

    public async Task<DisputePage> ListAsync(
        string? status,
        string? reason,
        int? limit,
        string? cursor,
        CancellationToken cancellationToken)
    {
        var take = limit ?? DefaultLimit;
        if (take < 1 || take > MaxLimit)
        {
            throw new RequestValidationException(
                FormattableString.Invariant($"limit must be between 1 and {MaxLimit}."));
        }

        var statusFilter = NormalizeStatus(status);
        var reasonFilter = NormalizeReason(reason);

        CursorPosition? position = null;
        if (!string.IsNullOrWhiteSpace(cursor))
        {
            try
            {
                position = Cursor.Decode(cursor);
            }
            catch (InvalidCursorException exception)
            {
                throw new RequestValidationException(exception.Message);
            }
        }

        var sql = new StringBuilder(
            """
            SELECT id AS "Id",
                   card_txn_ref AS "CardTxnRef",
                   merchant AS "Merchant",
                   amount_minor AS "AmountMinor",
                   currency AS "Currency",
                   reason_code AS "ReasonCode",
                   status AS "Status",
                   opened_at AS "OpenedAt",
                   respond_by AS "RespondBy",
                   xmin::text::bigint AS "Version"
            FROM disputes
            """);

        var arguments = new List<object>();
        var conditions = new List<string>();

        if (statusFilter is not null)
        {
            conditions.Add(FormattableString.Invariant($"status = {{{arguments.Count}}}"));
            arguments.Add(statusFilter);
        }

        if (reasonFilter is not null)
        {
            conditions.Add(FormattableString.Invariant($"reason_code = {{{arguments.Count}}}"));
            arguments.Add(reasonFilter);
        }

        if (position is { } keyset)
        {
            conditions.Add(FormattableString.Invariant(
                $"(opened_at, id) < ({{{arguments.Count}}}, {{{arguments.Count + 1}}})"));
            arguments.Add(keyset.OpenedAt);
            arguments.Add(keyset.Id);
        }

        if (conditions.Count > 0)
        {
            sql.Append(" WHERE ").Append(string.Join(" AND ", conditions));
        }

        sql.Append(" ORDER BY opened_at DESC, id DESC");
        sql.Append(FormattableString.Invariant($" LIMIT {{{arguments.Count}}}"));
        arguments.Add(take + 1);

        var rows = await _db.Database
            .SqlQueryRaw<DisputeListRow>(sql.ToString(), arguments.ToArray())
            .ToListAsync(cancellationToken);

        var hasMore = rows.Count > take;
        if (hasMore)
        {
            rows.RemoveRange(take, rows.Count - take);
        }

        var items = rows.Select(Map).ToList();

        string? nextCursor = null;
        if (hasMore && items.Count > 0)
        {
            var last = rows[^1];
            nextCursor = Cursor.Encode(new CursorPosition(last.OpenedAt, last.Id));
        }

        return new DisputePage(items, nextCursor);
    }

    private static DisputeListItem Map(DisputeListRow row) => new(
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

    private static string? NormalizeStatus(string? status)
    {
        if (string.IsNullOrWhiteSpace(status))
        {
            return null;
        }

        return status switch
        {
            "OPENED" => "OPENED",
            "EVIDENCE_SUBMITTED" => "EVIDENCE_SUBMITTED",
            "WON" => "WON",
            "LOST" => "LOST",
            "WITHDRAWN" => "WITHDRAWN",
            _ => throw new RequestValidationException("status is not a known dispute status.")
        };
    }

    private static string? NormalizeReason(string? reason)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            return null;
        }

        return reason switch
        {
            "FRAUD_CARD_ABSENT" => "FRAUD_CARD_ABSENT",
            "GOODS_NOT_RECEIVED" => "GOODS_NOT_RECEIVED",
            "DUPLICATE_CHARGE" => "DUPLICATE_CHARGE",
            "CANCELLED_RECURRING" => "CANCELLED_RECURRING",
            "CREDIT_NOT_PROCESSED" => "CREDIT_NOT_PROCESSED",
            _ => throw new RequestValidationException("reason is not a known reason code.")
        };
    }
}
