using System.Diagnostics;
using Ardalis.Result;
using MediatR;
using Microsoft.Extensions.Logging;

namespace TrackManagement.Application.Common.Behaviors;

/// <summary>Logs every command and query: name, outcome status and duration. Never logs payloads.</summary>
public sealed partial class LoggingBehavior<TRequest, TResponse>(ILogger<LoggingBehavior<TRequest, TResponse>> logger)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
    where TResponse : IResult
{
    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        var name = typeof(TRequest).Name;
        var started = Stopwatch.GetTimestamp();
        LogHandling(logger, name);

        var response = await next(cancellationToken);

        if (logger.IsEnabled(LogLevel.Information))
        {
            var status = response.Status;
            var elapsedMs = Stopwatch.GetElapsedTime(started).TotalMilliseconds;
            LogHandled(logger, name, status, elapsedMs);
        }
        return response;
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Handling {RequestName}")]
    private static partial void LogHandling(ILogger logger, string requestName);

    [LoggerMessage(Level = LogLevel.Information, Message = "Handled {RequestName} with {Status} in {ElapsedMs:0.0} ms")]
    private static partial void LogHandled(ILogger logger, string requestName, ResultStatus status, double elapsedMs);
}
