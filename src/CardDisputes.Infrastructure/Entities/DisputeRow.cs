namespace CardDisputes.Infrastructure.Entities;

public sealed class DisputeRow
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

    public string CreatedBy { get; set; } = string.Empty;

    public uint Version { get; set; }
}
