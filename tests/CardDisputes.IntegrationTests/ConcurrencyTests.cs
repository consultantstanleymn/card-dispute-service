using CardDisputes.Infrastructure.Entities;
using Microsoft.EntityFrameworkCore;

namespace CardDisputes.IntegrationTests;

#pragma warning disable CA1707

public sealed class ConcurrencyTests : IClassFixture<PostgresFixture>
{
    private readonly PostgresFixture _fixture;

    public ConcurrencyTests(PostgresFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Stale_Version_Throws_DbUpdateConcurrencyException()
    {
        await using (var setup = _fixture.CreateContext())
        {
            await setup.Database.MigrateAsync();
            setup.Disputes.Add(NewDispute());
            await setup.SaveChangesAsync();
        }

        Guid disputeId;
        await using (var read = _fixture.CreateContext())
        {
            disputeId = await read.Disputes.Select(dispute => dispute.Id).SingleAsync();
        }

        await using var first = _fixture.CreateContext();
        await using var second = _fixture.CreateContext();

        var firstCopy = await first.Disputes.SingleAsync(dispute => dispute.Id == disputeId);
        var secondCopy = await second.Disputes.SingleAsync(dispute => dispute.Id == disputeId);

        firstCopy.Status = "WITHDRAWN";
        await first.SaveChangesAsync();

        secondCopy.Status = "WITHDRAWN";

        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => second.SaveChangesAsync());
    }

    private static DisputeRow NewDispute() => new()
    {
        Id = Guid.NewGuid(),
        CardTxnRef = "txn-stale-version",
        Merchant = "Fictional Merchant",
        AmountMinor = 1500,
        Currency = "USD",
        ReasonCode = "GOODS_NOT_RECEIVED",
        Status = "OPENED",
        OpenedAt = DateTimeOffset.UtcNow,
        RespondBy = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(20),
        CreatedBy = "client-test"
    };
}
