using Cmf.Custom.Common.SD.Utilities.Helpers.Abstractions;
using Microsoft.Extensions.Logging;
using System.Diagnostics;

namespace Cmf.Custom.Common.SD.Utilities.Helpers;

/// <summary>
/// Initializes a new instance of the <see cref="CustomLoggerHelper<Scope>"/> class.
/// </summary>
public class CustomLoggerHelper(Foundation.Common.Logging.ILogger logger) : ICustomLoggerHelper
{
    protected Foundation.Common.Logging.ILogger _logger = logger;

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull
    {
        return _logger.BeginScope(state);
    }

    public bool IsEnabled(LogLevel logLevel)
    {
        return _logger.IsEnabled(logLevel);
    }

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        _logger.Log(logLevel, eventId, state, exception, formatter);
    }

    public void WriteLogDebug(string format, params object[] args)
    {
        _logger.WriteLogDebug(format, args);
    }

    public void WriteLogDebug(string message)
    {
        _logger.WriteLogDebug(message);
    }

    public void WriteLogEntry(Exception ex, TraceEventType severity, int priority, int eventId, string category)
    {
        _logger.WriteLogEntry(ex, severity, priority, eventId, category);
    }

    public void WriteLogEntry(string message, TraceEventType severity, int priority, int eventId, string category)
    {
        _logger.WriteLogEntry(message, severity, priority, eventId, category);
    }

    public void WriteLogEntry(string message, Exception error, TraceEventType severity, int priority, int eventId, string category)
    {
        _logger.WriteLogEntry(message, error, severity, priority, eventId, category);
    }

    public void WriteLogError(string format, params object[] args)
    {
        _logger.WriteLogError(format, args);
    }

    public void WriteLogError(string message)
    {
        _logger.WriteLogError(message);
    }

    public void WriteLogInformation(string format, params object[] args)
    {
        _logger.WriteLogInformation(format, args);
    }

    public void WriteLogInformation(string message)
    {
        _logger.WriteLogInformation(message);
    }

    public void WriteLogWarning(string format, params object[] args)
    {
        _logger.WriteLogWarning(format, args);
    }

    public void WriteLogWarning(string message)
    {
        _logger.WriteLogWarning(message);
    }
}