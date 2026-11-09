using CardDisputes.Infrastructure;

namespace CardDisputes.Api.Errors;

public sealed partial class ExceptionMappingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionMappingMiddleware> _logger;

    public ExceptionMappingMiddleware(RequestDelegate next, ILogger<ExceptionMappingMiddleware> logger)
    {
        ArgumentNullException.ThrowIfNull(next);
        ArgumentNullException.ThrowIfNull(logger);
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        try
        {
            await _next(context);
        }
        catch (Exception exception) when (!context.Response.HasStarted)
        {
            await MapAsync(context, exception);
        }
    }

    private Task MapAsync(HttpContext context, Exception exception) => exception switch
    {
        RequestValidationException validation => ProblemTypes.WriteAsync(
            context,
            400,
            ProblemTypes.Validation,
            "Request validation failed",
            validation.Message),

        DisputeNotFoundException notFound => ProblemTypes.WriteAsync(
            context,
            404,
            ProblemTypes.NotFound,
            "Dispute not found",
            notFound.Message),

        IllegalTransitionException illegal => ProblemTypes.WriteAsync(
            context,
            409,
            ProblemTypes.IllegalTransition,
            "Illegal transition",
            illegal.Message),

        ConcurrencyConflictException concurrency => ProblemTypes.WriteAsync(
            context,
            409,
            ProblemTypes.Concurrency,
            "Concurrency conflict",
            concurrency.Message),

        IdempotencyConflictException idempotency => ProblemTypes.WriteAsync(
            context,
            409,
            ProblemTypes.IdempotencyConflict,
            "Idempotency key conflict",
            idempotency.Message),

        _ => LogAndReturnInternalAsync(context, exception)
    };

    [LoggerMessage(Level = LogLevel.Error, Message = "Unhandled exception while processing {Path}.")]
    private static partial void LogUnhandled(ILogger logger, string path, Exception exception);

    private Task LogAndReturnInternalAsync(HttpContext context, Exception exception)
    {
        LogUnhandled(_logger, context.Request.Path, exception);
        return ProblemTypes.WriteAsync(
            context,
            500,
            ProblemTypes.Internal,
            "Internal server error");
    }
}
