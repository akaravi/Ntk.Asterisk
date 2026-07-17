using System.Collections.Concurrent;
using Ntk.Asterisk.WebApi.Contracts;

namespace Ntk.Asterisk.WebApi.Services;

/// <summary>
/// Forwards selected application logs into the live event feed via <see cref="LiveEventSinkHolder"/>.
/// </summary>
public sealed class LiveEventLoggerProvider : ILoggerProvider
{
    private readonly LiveEventSinkHolder _sink;
    private readonly ConcurrentDictionary<string, LiveEventLogger> _loggers = new(StringComparer.Ordinal);

    public LiveEventLoggerProvider(LiveEventSinkHolder sink)
    {
        _sink = sink;
    }

    public ILogger CreateLogger(string categoryName) =>
        _loggers.GetOrAdd(categoryName, name => new LiveEventLogger(name, _sink));

    public void Dispose() => _loggers.Clear();

    private sealed class LiveEventLogger : ILogger
    {
        private readonly string _category;
        private readonly LiveEventSinkHolder _sink;

        public LiveEventLogger(string category, LiveEventSinkHolder sink)
        {
            _category = category;
            _sink = sink;
        }

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel)
        {
            if (logLevel < LogLevel.Information)
                return false;
            if (_category.StartsWith("Ntk.Asterisk.WebApi.Services.AmiLiveEventFeed", StringComparison.Ordinal))
                return false;
            if (_category.StartsWith("Ntk.Asterisk", StringComparison.Ordinal))
                return logLevel >= LogLevel.Information;
            return logLevel >= LogLevel.Warning;
        }

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(logLevel))
                return;

            var message = formatter(state, exception);
            if (exception is not null)
                message = $"{message} | {exception.GetType().Name}: {exception.Message}";

            if (message.Length > 800)
                message = message[..800] + "…";

            _sink.TryPublish(new LiveEventDto
            {
                Id = Guid.NewGuid().ToString("N"),
                AtUtc = DateTimeOffset.UtcNow,
                Source = "app",
                Category = ShortCategory(_category),
                Level = logLevel.ToString(),
                Message = message
            });
        }

        private static string ShortCategory(string category)
        {
            var idx = category.LastIndexOf('.');
            return idx >= 0 && idx < category.Length - 1 ? category[(idx + 1)..] : category;
        }
    }
}
