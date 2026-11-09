using System.Text.Json;
using Microsoft.AspNetCore.Mvc;

namespace CardDisputes.Api.Errors;

public static class ProblemTypes
{
    public const string Validation = "/problems/validation";
    public const string NotFound = "/problems/not-found";
    public const string IllegalTransition = "/problems/illegal-transition";
    public const string Concurrency = "/problems/concurrency";
    public const string IdempotencyConflict = "/problems/idempotency-conflict";
    public const string Unauthorized = "/problems/unauthorized";
    public const string Internal = "/problems/internal";

    private const string ProblemContentType = "application/problem+json; charset=utf-8";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static Task WriteAsync(
        HttpContext context,
        int statusCode,
        string type,
        string title,
        string? detail = null)
    {
        ArgumentNullException.ThrowIfNull(context);

        var problem = new ProblemDetails
        {
            Type = type,
            Title = title,
            Status = statusCode,
            Detail = detail,
            Instance = context.Request.Path
        };

        return WriteAsync(context, problem);
    }

    public static async Task WriteAsync(HttpContext context, ProblemDetails problem)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(problem);

        context.Response.StatusCode = problem.Status ?? 500;
        context.Response.ContentType = ProblemContentType;
        await context.Response.WriteAsJsonAsync(problem, JsonOptions, ProblemContentType);
    }
}
