using System.Diagnostics;

namespace Cmf.Custom.Common.SD.Utilities.Helpers.Abstractions;

/// <summary>
/// Provides convenience methods for writing debug, information, warning, error, and event log entries.
/// </summary>
public interface ICustomLoggerHelper
{
    /// <summary>Writes a debug log entry using a format string and arguments.</summary>
    /// <param name="format">The format string for the log entry.</param>
    /// <param name="args">The values to format into the log entry.</param>
    void WriteLogDebug(string format, params object[] args);
    /// <summary>Writes a debug log entry.</summary>
    /// <param name="message">The message to log.</param>
    void WriteLogDebug(string message);
    /// <summary>Writes an exception as a log entry.</summary>
    /// <param name="ex">The exception to log.</param>
    /// <param name="severity">The severity of the log entry.</param>
    /// <param name="priority">The priority of the log entry.</param>
    /// <param name="eventId">The identifier of the log event.</param>
    /// <param name="category">The category of the log entry.</param>
    void WriteLogEntry(Exception ex, TraceEventType severity, int priority, int eventId, string category);
    /// <summary>Writes a message as a log entry.</summary>
    /// <param name="message">The message to log.</param>
    /// <param name="severity">The severity of the log entry.</param>
    /// <param name="priority">The priority of the log entry.</param>
    /// <param name="eventId">The identifier of the log event.</param>
    /// <param name="category">The category of the log entry.</param>
    void WriteLogEntry(string message, TraceEventType severity, int priority, int eventId, string category);
    /// <summary>Writes a message and its associated exception as a log entry.</summary>
    /// <param name="message">The message to log.</param>
    /// <param name="error">The exception associated with the message.</param>
    /// <param name="severity">The severity of the log entry.</param>
    /// <param name="priority">The priority of the log entry.</param>
    /// <param name="eventId">The identifier of the log event.</param>
    /// <param name="category">The category of the log entry.</param>
    void WriteLogEntry(string message, Exception error, TraceEventType severity, int priority, int eventId, string category);
    /// <summary>Writes an error log entry using a format string and arguments.</summary>
    /// <param name="format">The format string for the log entry.</param>
    /// <param name="args">The values to format into the log entry.</param>
    void WriteLogError(string format, params object[] args);
    /// <summary>Writes an error log entry.</summary>
    /// <param name="message">The message to log.</param>
    void WriteLogError(string message);
    /// <summary>Writes an information log entry using a format string and arguments.</summary>
    /// <param name="format">The format string for the log entry.</param>
    /// <param name="args">The values to format into the log entry.</param>
    void WriteLogInformation(string format, params object[] args);
    /// <summary>Writes an information log entry.</summary>
    /// <param name="message">The message to log.</param>
    void WriteLogInformation(string message);
    /// <summary>Writes a warning log entry using a format string and arguments.</summary>
    /// <param name="format">The format string for the log entry.</param>
    /// <param name="args">The values to format into the log entry.</param>
    void WriteLogWarning(string format, params object[] args);
    /// <summary>Writes a warning log entry.</summary>
    /// <param name="message">The message to log.</param>
    void WriteLogWarning(string message);
}