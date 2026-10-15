using System.Diagnostics;

namespace Cmf.Custom.Common.SD.Utilities.Helpers.Abstractions;

public interface ICustomLoggerHelper
{
    void WriteLogDebug(string format, params object[] args);
    void WriteLogDebug(string message);
    void WriteLogEntry(Exception ex, TraceEventType severity, int priority, int eventId, string category);
    void WriteLogEntry(string message, TraceEventType severity, int priority, int eventId, string category);
    void WriteLogEntry(string message, Exception error, TraceEventType severity, int priority, int eventId, string category);
    void WriteLogError(string format, params object[] args);
    void WriteLogError(string message);
    void WriteLogInformation(string format, params object[] args);
    void WriteLogInformation(string message);
    void WriteLogWarning(string format, params object[] args);
    void WriteLogWarning(string message);
}