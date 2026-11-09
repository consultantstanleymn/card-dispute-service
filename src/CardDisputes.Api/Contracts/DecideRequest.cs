namespace CardDisputes.Api.Contracts;

public sealed record DecideRequest
{
    public string? Outcome { get; init; }

    public long Version { get; init; }
}
