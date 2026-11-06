using System.Diagnostics;
using System.Text;
using Cmf.Custom.Common.SD.Utilities.Helpers.Abstractions;
using Cmf.Foundation.BusinessObjects;
using Cmf.Foundation.BusinessObjects.Abstractions;
using Cmf.Foundation.Common.Abstractions;

namespace Cmf.Custom.Common.SD.Utilities.Helpers;

public class LogHelpers(IEntityFactory entityFactory, INotification notification) : ILogHelpers
{
    private const string DELIMITER = " | ";
    private const int MAX_LOG_SIZE = 1_000_000;

    private Stopwatch _stopwatch;
    private StringBuilder _log;
    private DateTime _loggingStartTime;
    private bool _isLogging;

    private readonly IEntityFactory _entityFactory = entityFactory;
    private INotification _notification = notification;

    /// <inheritdoc/>
    public void StartLogging(string startMessage = null, bool logToNotifications = false)
    {
        if (_isLogging)
        {
            Cmf.Foundation.Common.Utilities.WriteLogInformation(
                $"LogUtilities: Logging already active. Stopping previous session and starting new one.");
            StopLoggingInternal();  // Clean up previous session
        }

        _stopwatch = Stopwatch.StartNew();
        _loggingStartTime = DateTime.Now;
        _isLogging = true;
        _notification = null;  // Reset notification

        if (logToNotifications)
        {
            _log = new StringBuilder();
        }

        WriteLogEntryInternal($"Logging started {startMessage ?? string.Empty}");
    }

    /// <inheritdoc/>
    public void WriteLog(string logMessage, bool restartTimer = false)
    {
        if (string.IsNullOrWhiteSpace(logMessage))
        {
            return;
        }

        if (!LogWarningIfNotStarted())
        {
            return;  // Not logging, skip
        }

        try
        {
            double elapsed = _stopwatch.Elapsed.TotalSeconds;
            string formattedMessage = $"{logMessage}: {elapsed:F3}";

            Cmf.Foundation.Common.Utilities.WriteLogInformation($"Debug {formattedMessage}");

            if (_log != null)
            {
                CheckAndTruncateLogIfNeeded(formattedMessage);
                _log.Append(formattedMessage);
                _log.Append(DELIMITER);
            }

            if (restartTimer)
            {
                _stopwatch.Restart();
            }
            else
            {
                _stopwatch.Reset();
            }
        }
        catch (Exception ex)
        {
            Cmf.Foundation.Common.Utilities.WriteLogInformation(
                $"Error writing log: {ex.Message}", ex);
        }
    }
    
    /// <inheritdoc/>
    public void StartTimer()
    {
        if (!LogWarningIfNotStarted())
        {
            return;
        }
        _stopwatch?.Start();
    }
    
    /// <inheritdoc/>
    public void WriteLogEntry(string logMessage)
    {
        if (string.IsNullOrWhiteSpace(logMessage))
        {
            return;
        }

        WriteLogEntryInternal(logMessage);
    }

    /// <inheritdoc/>
    private void WriteLogEntryInternal(string logMessage)
    {
        if (!LogWarningIfNotStarted())
        {
            return;
        }

        try
        {
            string formattedMessage = $"{logMessage}";
            Cmf.Foundation.Common.Utilities.WriteLogInformation($"Debug {formattedMessage}");

            if (_log != null)
            {
                CheckAndTruncateLogIfNeeded(formattedMessage);
                _log.Append(formattedMessage);
                _log.Append(DELIMITER);
            }
        }
        catch (Exception ex)
        {
            Cmf.Foundation.Common.Utilities.WriteLogInformation(
                $"LogUtilities: Error writing log entry: {ex.Message}", ex);
        }
    }

    /// <inheritdoc/>
    public string StopLogging(string stopMessage = null)
    {
        return StopLoggingInternal(stopMessage);
    }

    /// <inheritdoc/>
    private string StopLoggingInternal(string stopMessage = null)
    {
        if (!_isLogging)
        {
            Cmf.Foundation.Common.Utilities.WriteLogInformation(
                "LogUtilities: StopLogging called but logging is not active.");
            return string.Empty;
        }

        _stopwatch?.Stop();
        DateTime stopTime = DateTime.Now;
        double totalTime = (stopTime - _loggingStartTime).TotalSeconds;

        WriteLogEntryInternal($"Logging stopped. Total elapsed time: {totalTime:F3} seconds. {stopMessage ?? string.Empty}");

        string result = _log?.ToString() ?? string.Empty;

        // Reset state for reuse
        _isLogging = false;
        _stopwatch = null;

        return result;
    }

    /// <inheritdoc/>
    public string GetCurrentLog()
    {
        return _log?.ToString() ?? string.Empty;
    }

    /// <inheritdoc/>
    public void StoreLogsInNotification(string name, string description, int validityDays = 1)
    {
        if (_log == null || _log.Length == 0)
        {
            Cmf.Foundation.Common.Utilities.WriteLogInformation(
                "LogUtilities: No logs to store in notification.");
            return;
        }

        string notificationName = !string.IsNullOrWhiteSpace(name)
            ? name
            : $"Log-{Guid.NewGuid()}";

        INotification notification = _entityFactory.Create<INotification>();
        notification.Name = notificationName;
        notification.Type = "Generic";
        notification.Title = notificationName;
        notification.Details = _log.ToString();
        notification.Description = description ?? "System log notification";
        notification.Severity = "Normal";
        notification.SendEmailToAssignedParty = false;
        notification.AssignmentType = NotificationAssignmentType.Everyone;
        notification.ClearanceMode = ClearanceMode.ManualEveryUser;
        notification.ValidTo = DateTime.Now.AddDays(Math.Max(1, validityDays));

        try
        {
            notification.Create();
            _notification = notification;  // Store reference
        }
        catch (Exception ex)
        {
            Cmf.Foundation.Common.Utilities.WriteLogInformation(
                $"LogUtilities: Failed to create log notification: {ex.Message}", ex);
            throw;
        }
    }

    /// <inheritdoc/>
    public void StoreLogsInNotification(INotification notification)
    {
        if (notification == null)
        {
            Cmf.Foundation.Common.Utilities.WriteLogInformation(
                "LogUtilities: Cannot store logs - notification is null");
            return;
        }

        if (_log == null || _log.Length == 0)
        {
            Cmf.Foundation.Common.Utilities.WriteLogInformation(
                "LogUtilities: No logs to store in notification.");
            return;
        }

        notification.Details = _log.ToString();

        try
        {
            notification.Create();
            _notification = notification;
        }
        catch (Exception ex)
        {
            Cmf.Foundation.Common.Utilities.WriteLogInformation(
                $"LogUtilities: Failed to create notification: {ex.Message}", ex);
            throw;
        }
    }

    /// <inheritdoc/>
    public void ClearLog()
    {
        _log?.Clear();
    }
    
    /// <inheritdoc/>
    public void Reset()
    {
        _stopwatch?.Stop();
        _stopwatch = null;
        _log?.Clear();
        _log = null;
        _notification = null;
        _isLogging = false;
        _loggingStartTime = default;
    }

    /// <inheritdoc/>
    private bool LogWarningIfNotStarted()
    {
        if (!_isLogging)
        {
            Cmf.Foundation.Common.Utilities.WriteLogInformation(
                "LogUtilities: Logging has not been started. Call StartLogging() first.");
            return false;
        }
        return true;
    }

    /// <inheritdoc/>
    private void CheckAndTruncateLogIfNeeded(string newMessage)
    {
        if (_log == null) { return; }

        if (_log.Length + newMessage.Length > MAX_LOG_SIZE)
        {
            Cmf.Foundation.Common.Utilities.WriteLogInformation(
                "LogUtilities: Log size limit reached. Truncating old entries.");

            int keepLength = (int)(MAX_LOG_SIZE * 0.75);

            if (_log.Length > keepLength)
            {
                int removeLength = _log.Length - keepLength;
                _log.Remove(0, removeLength);
            }

            _log.Insert(0, "[... earlier entries truncated ...]\n");
        }
    }
}