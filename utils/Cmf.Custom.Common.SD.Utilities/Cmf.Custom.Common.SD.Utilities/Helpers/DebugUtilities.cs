using System.Diagnostics;
using System.IO;
using System.Text;

namespace Cmf.Custom.Common.SD.Utilities.Helpers
{
    /// <summary>
    /// Provides simple file logging and debugger utilities for diagnostics.
    /// </summary>
    public static class DebugUtilities
    {
        public static void LaunchDebugger(object debugContext = null, Exception error = null)
        {
            Debugger.Break();
        }

        public static void Log(string ruleName, string message, string filePath = "CustomActionUtilities.log")
        {
            StringBuilder logBuilder = new();
            logBuilder.AppendLine("-----------------------------");
            logBuilder.Append("Date: ").AppendLine(DateTime.Now.ToString("yyyy/MM/dd HH:mm:ss.fff"));
            logBuilder.Append("Rule: ").AppendLine(ruleName);
            logBuilder.Append("Mesg: ").AppendLine(message);
            logBuilder.AppendLine("-----------------------------");

            File.AppendAllText(filePath, logBuilder.ToString());
        }
    }
}
