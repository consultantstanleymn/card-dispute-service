using CardDisputes.Infrastructure.Entities;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace CardDisputes.IntegrationTests;

#pragma warning disable CA1707

public sealed class EventsAppendOnlyTests : IClassFixture<PostgresFixture>
{
    private readonly PostgresFixture _fixture;

    public EventsAppendOnlyTests(PostgresFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Update_On_Events_Is_Rejected()
    {
        var eventId = await CreateEvent();

        await using var context = _fixture.CreateContext();
        var exception = await Assert.ThrowsAsync<PostgresException>(() =>
            context.Database.ExecuteSqlRawAsync(
                "update dispute_events set to_status = 'WON' where id = {0}",
                eventId));

        Assert.Contains("append-only", exception.MessageText, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Delete_On_Events_Is_Rejected()
    {
        var eventId = await CreateEvent();

        await using var context = _fixture.CreateContext();
        var exception = await Assert.ThrowsAsync<PostgresException>(() =>
            context.Database.ExecuteSqlRawAsync(
                "delete from dispute_events where id = {0}",
                eventId));

        Assert.Contains("append-only", exception.MessageText, StringComparison.Ordinal);
    }

    private async Task<Guid> CreateEvent()
    {
        await using var context = _fixture.CreateContext();
        await context.Database.MigrateAsync();

        var dispute = new DisputeRow
        {
            Id = Guid.NewGuid(),
            CardTxnRef = "txn-append-only",
            Merchant = "Fictional Merchant",
            AmountMinor = 2500,
            Currency = "USD",
            ReasonCode = "FRAUD_CARD_ABSENT",
            Status = "OPENED",
            OpenedAt = DateTimeOffset.UtcNow,
            RespondBy = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(30),
            CreatedBy = "client-test"
        };

        var disputeEvent = new DisputeEvent
        {
            Id = Guid.NewGuid(),
            DisputeId = dispute.Id,
            FromStatus = null,
            ToStatus = "OPENED",
            ClientId = "client-test",
            OccurredAt = DateTimeOffset.UtcNow
        };

        context.Disputes.Add(dispute);
        context.DisputeEvents.Add(disputeEvent);
        await context.SaveChangesAsync();

        return disputeEvent.Id;
    }
}
