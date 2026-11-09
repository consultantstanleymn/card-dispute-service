namespace CardDisputes.Infrastructure.Entities;

public sealed class DisputeEvent
{
    public Guid Id { get; set; }

    public Guid DisputeId { get; set; }

    public string? FromStatus { get; set; }

    public string ToStatus { get; set; } = string.Empty;

    public string ClientId { get; set; } = string.Empty;

    public DateTimeOffset OccurredAt { get; set; }
}
