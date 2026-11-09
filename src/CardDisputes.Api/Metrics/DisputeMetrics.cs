using CardDisputes.Infrastructure;
using Prometheus;

namespace CardDisputes.Api.Metrics;

public sealed class DisputeMetrics : IDisputeMetrics
{
    // Static, because the default registry accepts each metric name once per process.
    private static readonly Counter OpenedCounter = Prometheus.Metrics.CreateCounter(
        "cds_disputes_opened_total",
        "Number of disputes opened.",
        new CounterConfiguration { LabelNames = new[] { "reason" } });

    private static readonly Counter TransitionsCounter = Prometheus.Metrics.CreateCounter(
        "cds_dispute_transitions_total",
        "Number of dispute state transitions.",
        new CounterConfiguration { LabelNames = new[] { "from", "to" } });

    private static readonly Histogram UploadBytes = Prometheus.Metrics.CreateHistogram(
        "cds_evidence_upload_bytes",
        "Size of accepted evidence uploads in bytes.",
        new HistogramConfiguration
        {
            Buckets = new[] { 1024d, 10_240d, 102_400d, 1_048_576d, 5_242_880d }
        });

    public Counter DisputeOpenedTotal => OpenedCounter;
    public Counter DisputeTransitionsTotal => TransitionsCounter;
    public Histogram EvidenceUploadBytes => UploadBytes;

    public void RecordOpened(string reason)
    {
        DisputeOpenedTotal.WithLabels(reason).Inc();
    }

    public void RecordTransition(string fromStatus, string toStatus)
    {
        DisputeTransitionsTotal.WithLabels(fromStatus, toStatus).Inc();
    }
}
