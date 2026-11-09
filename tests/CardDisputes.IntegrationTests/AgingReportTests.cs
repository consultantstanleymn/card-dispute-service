using CardDisputes.Infrastructure;
using CardDisputes.Infrastructure.Entities;
using Microsoft.EntityFrameworkCore;

namespace CardDisputes.IntegrationTests;

#pragma warning disable CA1707

public sealed class AgingReportTests : IClassFixture<PostgresFixture>
{
    private static readonly DateOnly Today = new(2026, 3, 10);

    private readonly PostgresFixture _fixture;

    public AgingReportTests(PostgresFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Aging_Buckets_Count_Only_Open_Disputes()
    {
        await using var context = _fixture.CreateContext();
        await context.Database.MigrateAsync();
        await context.Database.ExecuteSqlRawAsync(
            "truncate table dispute_events, evidence_files, idempotency_keys, disputes cascade");

        context.Disputes.AddRange(
            NewRow(Today.AddDays(-5), "OPENED", "FRAUD_CARD_ABSENT"),
            NewRow(Today.AddDays(-1), "EVIDENCE_SUBMITTED", "GOODS_NOT_RECEIVED"),
            NewRow(Today, "OPENED", "GOODS_NOT_RECEIVED"),
            NewRow(Today.AddDays(3), "OPENED", "DUPLICATE_CHARGE"),
            NewRow(Today.AddDays(4), "OPENED", "CANCELLED_RECURRING"),
            NewRow(Today.AddDays(7), "OPENED", "CREDIT_NOT_PROCESSED"),
            NewRow(Today.AddDays(8), "OPENED", "FRAUD_CARD_ABSENT"),
            NewRow(Today.AddDays(30), "OPENED", "FRAUD_CARD_ABSENT"),
            NewRow(Today.AddDays(-2), "WON", "FRAUD_CARD_ABSENT"),
            NewRow(Today.AddDays(2), "LOST", "GOODS_NOT_RECEIVED"));
        await context.SaveChangesAsync();

        var clock = new FixedTimeProvider(
            new DateTimeOffset(Today.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero));

        var query = new AgingReportQuery(context, clock);
        var report = await query.ExecuteAsync(CancellationToken.None);

        Assert.Equal(Today, report.AsOf);
        Assert.Equal(8, report.Buckets.Sum(bucket => bucket.Count));
        Assert.Equal(2, BucketCount(report, "overdue"));
        Assert.Equal(2, BucketCount(report, "0-3"));
        Assert.Equal(2, BucketCount(report, "4-7"));
        Assert.Equal(2, BucketCount(report, "8+"));

        var fraudAged = report.Buckets.Single(
            bucket => bucket.Reason == "FRAUD_CARD_ABSENT" && bucket.Bucket == "8+");
        Assert.Equal(2, fraudAged.Count);

        Assert.DoesNotContain(report.Buckets, bucket => bucket.Count == 0);
    }

    private static long BucketCount(AgingReport report, string bucket) =>
        report.Buckets
            .Where(row => string.Equals(row.Bucket, bucket, StringComparison.Ordinal))
            .Sum(row => row.Count);

    private static DisputeRow NewRow(DateOnly respondBy, string status, string reason) => new()
    {
        Id = Guid.NewGuid(),
        CardTxnRef = $"txn-{Guid.NewGuid():N}",
        Merchant = "Fictional Merchant",
        AmountMinor = 1500,
        Currency = "USD",
        ReasonCode = reason,
        Status = status,
        OpenedAt = new DateTimeOffset(respondBy.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero),
        RespondBy = respondBy,
        CreatedBy = ApiFactory.ClientId
    };

    private sealed class FixedTimeProvider : TimeProvider
    {
        private readonly DateTimeOffset _now;

        public FixedTimeProvider(DateTimeOffset now)
        {
            _now = now;
        }

        public override DateTimeOffset GetUtcNow() => _now;
    }
}
