namespace CardDisputes.Infrastructure.Entities;

public sealed class EvidenceFile
{
    public Guid Id { get; set; }

    public Guid DisputeId { get; set; }

    public string FileName { get; set; } = string.Empty;

    public string ContentType { get; set; } = string.Empty;

    public long SizeBytes { get; set; }

    public string Sha256 { get; set; } = string.Empty;

    public string ObjectKey { get; set; } = string.Empty;

    public DateTimeOffset UploadedAt { get; set; }
}
