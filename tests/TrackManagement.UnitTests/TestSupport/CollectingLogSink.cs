using System.Collections.Concurrent;
using System.Globalization;
using Serilog.Core;
using Serilog.Events;
using Microsoft.Extensions.Logging;

namespace TrackManagement.UnitTests.TestSupport;

/// <summary>Serilog sink (picked up by ReadFrom.Services) that keeps rendered log lines for assertions.</summary>
public sealed class CollectingLogSink : ILogEventSink
{
    private readonly ConcurrentQueue<string> _lines = new();

    public IReadOnlyList<string> Lines => [.. _lines];

    public void Emit(LogEvent logEvent)
    {
        var properties = string.Join(" ", logEvent.Properties.Select(p => $"{p.Key}={p.Value}"));
        _lines.Enqueue($"{logEvent.RenderMessage(CultureInfo.InvariantCulture)} | {properties} | {logEvent.Exception}");
    }
}

/// <summary>Microsoft.Extensions.Logging provider that keeps formatted messages, for service-level tests.</summary>
public sealed class ListLoggerProvider : ILoggerProvider
{
    private readonly ConcurrentQueue<(LogLevel Level, int EventId, string Message)> _entries = new();

    public IReadOnlyList<(LogLevel Level, int EventId, string Message)> Entries => [.. _entries];

    public ILogger CreateLogger(string categoryName) => new ListLogger(_entries);

    public void Dispose()
    {
    }

    private sealed class ListLogger(ConcurrentQueue<(LogLevel, int, string)> entries) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
            entries.Enqueue((logLevel, eventId.Id, formatter(state, exception)));
    }
}
