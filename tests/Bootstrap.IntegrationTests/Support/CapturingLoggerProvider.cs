using Microsoft.Extensions.Logging;

namespace MyApp.Tests.Bootstrap.Support;

public sealed record CapturedLog(string Category, LogLevel Level, string Message);

/// <summary>
/// Guarda en memoria lo que la app registra por <see cref="ILogger"/>, para poder afirmar sobre
/// los niveles emitidos (por ejemplo "esta respuesta 4xx no generó ningún ERROR").
/// </summary>
public sealed class CapturingLoggerProvider : ILoggerProvider
{
    private readonly List<CapturedLog> entries = [];

    public IReadOnlyList<CapturedLog> Snapshot()
    {
        lock (entries)
        {
            return entries.ToList();
        }
    }

    public void Clear()
    {
        lock (entries)
        {
            entries.Clear();
        }
    }

    public ILogger CreateLogger(string categoryName) => new CapturingLogger(categoryName, this);

    public void Dispose()
    {
    }

    private void Add(CapturedLog entry)
    {
        lock (entries)
        {
            entries.Add(entry);
        }
    }

    private sealed class CapturingLogger(string category, CapturingLoggerProvider owner) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => logLevel != LogLevel.None;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            var message = formatter(state, exception);
            owner.Add(new CapturedLog(category, logLevel, exception is null ? message : $"{message} | {exception.GetType().Name}: {exception.Message}"));
        }
    }
}
