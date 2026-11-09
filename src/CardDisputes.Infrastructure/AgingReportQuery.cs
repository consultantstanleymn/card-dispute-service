using Microsoft.EntityFrameworkCore;

namespace CardDisputes.Infrastructure;

public sealed record AgingBucket(string Reason, string Bucket, long Count);

public sealed record AgingReport(DateOnly AsOf, IReadOnlyList<AgingBucket> Buckets);

public sealed class AgingBucketRow
{
    public string Reason { get; set; } = string.Empty;

    public string Bucket { get; set; } = string.Empty;

    public long Count { get; set; }
}

public sealed class AgingReportQuery
{
    private readonly DisputesDbContext _db;
    private readonly TimeProvider _timeProvider;

    public AgingReportQuery(DisputesDbContext db, TimeProvider? timeProvider = null)
    {
        ArgumentNullException.ThrowIfNull(db);
        _db = db;
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public async Task<AgingReport> ExecuteAsync(CancellationToken cancellationToken)
    {
        var asOf = DateOnly.FromDateTime(_timeProvider.GetUtcNow().UtcDateTime);

        var rows = await _db.Database
            .SqlQuery<AgingBucketRow>(
                $"""
                SELECT "Reason", "Bucket", count(*) AS "Count"
                FROM (
                    SELECT reason_code AS "Reason",
                           CASE
                               WHEN respond_by < {asOf} THEN 'overdue'
                               WHEN respond_by <= {asOf} + 3 THEN '0-3'
                               WHEN respond_by <= {asOf} + 7 THEN '4-7'
                               ELSE '8+'
                           END AS "Bucket"
                    FROM disputes
                    WHERE status IN ('OPENED', 'EVIDENCE_SUBMITTED')
                ) AS open_disputes
                GROUP BY "Reason", "Bucket"
                ORDER BY "Reason",
                         CASE "Bucket"
                             WHEN 'overdue' THEN 0
                             WHEN '0-3' THEN 1
                             WHEN '4-7' THEN 2
                             ELSE 3
                         END
                """)
            .ToListAsync(cancellationToken);

        var buckets = rows
            .Select(row => new AgingBucket(row.Reason, row.Bucket, row.Count))
            .ToList();

        return new AgingReport(asOf, buckets);
    }
}
