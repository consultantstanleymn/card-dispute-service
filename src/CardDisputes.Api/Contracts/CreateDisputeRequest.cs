namespace CardDisputes.Api.Contracts;

public sealed record CreateDisputeRequest
{
    public string? CardTxnRef { get; init; }

    public string? Merchant { get; init; }

    public long AmountMinor { get; init; }

    public string? Currency { get; init; }

    public string? Reason { get; init; }

    public string? ReasonCode { get; init; }
}
