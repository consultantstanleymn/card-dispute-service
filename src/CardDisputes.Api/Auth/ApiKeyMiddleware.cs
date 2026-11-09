using CardDisputes.Api.Errors;

namespace CardDisputes.Api.Auth;

public sealed class ApiKeyMiddleware
{
    public const string ClientIdItemKey = "cds.client_id";

    private readonly RequestDelegate _next;
    private readonly Dictionary<string, string> _keys;

    public ApiKeyMiddleware(RequestDelegate next, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(next);
        ArgumentNullException.ThrowIfNull(configuration);

        _next = next;
        _keys = configuration
            .GetSection("ApiKeys")
            .GetChildren()
            .Where(child => !string.IsNullOrWhiteSpace(child.Value))
            .ToDictionary(child => child.Key, child => child.Value!, StringComparer.Ordinal);
    }

    public async Task InvokeAsync(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (context.Request.Path.StartsWithSegments("/health")
            || context.Request.Path.StartsWithSegments("/metrics"))
        {
            await _next(context);
            return;
        }

        if (!context.Request.Headers.TryGetValue("X-Api-Key", out var supplied)
            || supplied.Count != 1
            || string.IsNullOrWhiteSpace(supplied[0]))
        {
            await ProblemTypes.WriteAsync(
                context,
                401,
                ProblemTypes.Unauthorized,
                "Missing API key",
                "The X-Api-Key header is required.");
            return;
        }

        if (!_keys.TryGetValue(supplied[0]!, out var clientId))
        {
            await ProblemTypes.WriteAsync(
                context,
                401,
                ProblemTypes.Unauthorized,
                "Unknown API key",
                "The X-Api-Key header is not known.");
            return;
        }

        context.Items[ClientIdItemKey] = clientId;
        await _next(context);
    }
}
