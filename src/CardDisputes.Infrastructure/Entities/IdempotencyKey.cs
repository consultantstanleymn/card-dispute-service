namespace CardDisputes.Infrastructure.Entities;

public sealed class IdempotencyKey
{
    public string ClientId { get; set; } = string.Empty;

    public string Key { get; set; } = string.Empty;

    public string RequestHash { get; set; } = string.Empty;

    public int StatusCode { get; set; }

    public string ResponseBody { get; set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; set; }
}
