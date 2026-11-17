using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace TrackManagement.Api.ExceptionHandlers;

/// <summary>
/// Last-resort handler: logs the full exception server-side and returns a generic 500
/// ProblemDetails (with traceId) that never exposes exception messages or stack traces.
/// </summary>
public sealed partial class GlobalExceptionHandler(
    IProblemDetailsService problemDetailsService,
    ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        LogUnhandled(logger, exception, httpContext.Request.Method, httpContext.Request.Path, httpContext.TraceIdentifier);

        httpContext.Response.StatusCode = StatusCodes.Status500InternalServerError;
        return await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            Exception = exception,
            ProblemDetails = new ProblemDetails
            {
                Status = StatusCodes.Status500InternalServerError,
                Title = "An unexpected error occurred",
                Detail = "An unexpected error occurred. Please try again later.",
            },
        });
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Unhandled exception on {Method} {Path} (traceId {TraceId})")]
    private static partial void LogUnhandled(ILogger logger, Exception exception, string method, PathString path, string traceId);
}
