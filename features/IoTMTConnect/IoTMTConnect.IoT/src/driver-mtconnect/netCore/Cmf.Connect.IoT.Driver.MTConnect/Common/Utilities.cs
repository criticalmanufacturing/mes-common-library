using System;
using System.Threading;

namespace Cmf.Connect.IoT.Driver.MTConnect.Common
{
    public class Utilities
    {
        /// <summary>Wait for something to happen. Throws an exception if the condition is not met</summary>
        /// <param name="timeout">Number of seconds to wait</param>
        /// <param name="errorMessage">Error text that will be returned as an exception</param>
        /// <param name="code">Function to evaluate the condition</param>
        public static void WaitFor(float timeout, string errorMessage, Func<bool> code)
        {
            WaitFor(timeout, code, errorMessage);
        }

        /// <summary>Wait for something to happen. Throws an exception if the condition is not met</summary>
        /// <param name="timeout">Number of seconds to wait</param>
        /// <param name="code">Function to evaluate the condition</param>
        /// <param name="errorMessage">Error text that will be returned as an exception</param>
        /// <param name="args">Arguments to pass to the error message</param>
        public static void WaitFor(float timeout, Func<bool> code, string errorMessage, params object[] args)
        {
            if (code == null)
                return;

            while (timeout-- > 0)
            {
                if (code())
                    return;

                // Wait 1 second for next try
                Thread.Sleep(1000);
            }

            // Got here? then the timeout has been reached
            throw new Exception(DateTime.UtcNow.ToString("HH:mm:ss.fffff") + ": " + string.Format(errorMessage, args));
        }
    }
}
