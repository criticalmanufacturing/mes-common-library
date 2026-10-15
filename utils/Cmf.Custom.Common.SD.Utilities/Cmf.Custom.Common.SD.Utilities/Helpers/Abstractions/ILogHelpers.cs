using Cmf.Foundation.BusinessObjects.Abstractions;

namespace Cmf.Custom.Common.SD.Utilities.Helpers.Abstractions;

/// <summary>
/// Utility interface for logging operations with timing and notification storage.
/// Notification logging (storing logs in an MES Notification) is controlled by a configuration value
/// or the logToNotifications parameter passed to StartLogging().
///
/// Log entries are automatically truncated if they exceed the maximum size limit (1MB).
/// </summary>
public interface ILogHelpers
{
    /// <summary>
    /// Starts the logging process by initializing and starting a stopwatch.
    /// If notification logging is enabled (via logToNotifications parameter or configuration),
    /// initializes the log buffer to store entries in memory.
    ///
    /// If logging is already active, stops the previous session and starts a new one,
    /// clearing any accumulated logs.
    /// </summary>
    /// <param name="startMessage">Optional message to append to the start log entry.</param>
    /// <param name="logToNotifications">
    /// If true, enables in-memory log buffering.
    /// If false, buffering is controlled by configuration (currently disabled - pending implementation).
    /// </param>
    void StartLogging(string startMessage = null, bool logToNotifications = false);

    /// <summary>
    /// Logs a message with the elapsed time (in seconds with 3 decimal places) since the
    /// last WriteLog() call or StartTimer().
    ///
    /// By default, the timer is reset (stopped) after logging. Set restartTimer to true
    /// to automatically restart the timer for consecutive measurements.
    ///
    /// If the log buffer exceeds the maximum size (1MB), older entries are automatically
    /// truncated to maintain the size limit.
    ///
    /// If an error occurs during logging, it is caught and logged but does not propagate
    /// to prevent logging failures from breaking application flow.
    ///
    /// Logs a warning if StartLogging() has not been called and skips the log entry.
    ///
    /// Format: "{logMessage}: {elapsedTime:F3}"
    /// </summary>
    /// <param name="logMessage">The message to log. Ignored if null or whitespace.</param>
    /// <param name="restartTimer">
    /// If true, restarts the timer immediately after logging for the next measurement.
    /// If false (default), resets and stops the timer. Call StartTimer() to resume timing.
    /// </param>
    void WriteLog(string logMessage, bool restartTimer = false);

    /// <summary>
    /// Starts (or resumes) the internal stopwatch for timing measurements.
    /// Use this to begin timing before the next WriteLog() call.
    ///
    /// Logs a warning if StartLogging() has not been called and skips the operation.
    /// </summary>
    void StartTimer();

    /// <summary>
    /// Writes a log entry without elapsed time information.
    ///
    /// If the log buffer exceeds the maximum size (1MB), older entries are automatically
    /// truncated to maintain the size limit.
    ///
    /// Logs a warning if StartLogging() has not been called and skips the log entry.
    ///
    /// Format: "{logMessage}"
    /// </summary>
    /// <param name="logMessage">The message to log. Ignored if null or whitespace.</param>
    void WriteLogEntry(string logMessage);

    /// <summary>
    /// Stops the logging process, calculates total elapsed time since StartLogging(),
    /// and returns the complete accumulated log (if notification logging was enabled).
    ///
    /// After stopping, the internal state is reset (stopwatch and isLogging flag), but the
    /// log buffer is preserved to allow retrieval via GetCurrentLog() or storage via
    /// StoreLogsInNotification(). Call ClearLog() or Reset() to discard the logs.
    ///
    /// The logger instance can be reused by calling StartLogging() again.
    ///
    /// Logs a warning if StartLogging() has not been called.
    /// </summary>
    /// <param name="stopMessage">Optional message to append to the stop log entry.</param>
    /// <returns>
    /// The complete log as a pipe-delimited string if notification logging was enabled,
    /// or empty string if notification logging is disabled or no logs were captured.
    /// </returns>
    string StopLogging(string stopMessage = null);

    /// <summary>
    /// Gets the current accumulated log without stopping the logging process.
    /// </summary>
    /// <returns>
    /// The current log as a pipe-delimited string if notification logging is enabled,
    /// or empty string if notification logging is disabled.
    /// </returns>
    string GetCurrentLog();

    /// <summary>
    /// Stores the accumulated logs in a new notification and creates it in the system.
    ///
    /// This method can be called multiple times to create multiple notifications with the same logs.
    /// If no logs are available (notification logging disabled or buffer empty), the method
    /// returns without creating a notification.
    ///
    /// The notification is configured with:
    /// - Type: "Generic"
    /// - Severity: "Normal"
    /// - AssignmentType: Everyone
    /// - ClearanceMode: ManualEveryUser
    /// - SendEmailToAssignedParty: false
    /// </summary>
    /// <param name="name">
    /// The name and title for the notification.
    /// If not provided or empty, generates a GUID-based name (format: "Log-{guid}").
    /// </param>
    /// <param name="description">
    /// The description for the notification.
    /// Defaults to "System log notification" if not provided or empty.
    /// </param>
    /// <param name="validityDays">
    /// Number of days the notification should remain valid.
    /// Minimum value is 1. Default is 1 day.
    /// </param>
    /// <exception cref="Exception">
    /// Rethrows any exceptions that occur during notification creation.
    /// Logs the error before rethrowing.
    /// </exception>
    void StoreLogsInNotification(string name, string description, int validityDays = 1);

    /// <summary>
    /// Stores the accumulated logs in the provided notification's Details property and creates it.
    ///
    /// The notification must be pre-configured with all required properties except Details,
    /// which is set by this method.
    ///
    /// If notification logging is disabled or no logs are available, returns without
    /// creating the notification and logs a warning.
    /// </summary>
    /// <param name="notification">
    /// Pre-configured notification to use for storing logs.
    /// Must not be null. The Details property will be overwritten with the log contents.
    /// </param>
    /// <exception cref="Exception">
    /// Rethrows any exceptions that occur during notification creation.
    /// Logs the error before rethrowing.
    /// </exception>
    void StoreLogsInNotification(INotification notification);

    /// <summary>
    /// Clears the current log buffer if notification logging is enabled.
    /// Does not affect the logging state (isLogging flag) or stopwatch.
    ///
    /// Use this if you want to discard accumulated logs but continue logging new entries.
    /// </summary>
    void ClearLog();

    /// <summary>
    /// Completely resets the logger to its initial state, clearing all internal data.
    /// This includes:
    /// - Stopping and clearing the stopwatch
    /// - Clearing the log buffer
    /// - Resetting the notification reference
    /// - Resetting the logging state flag
    /// - Clearing the start time
    ///
    /// After calling Reset(), the logger can be reused by calling StartLogging() again.
    /// This is useful when reusing a logger instance across multiple operations.
    /// </summary>
    void Reset();
}