using Cmf.CLI.Core;
using Cmf.CLI.Core.Enums;
using Cmf.CLI.Utilities;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO.Abstractions;
using System.Linq;

namespace audit.Objects
{
    /// <summary>
    /// Runs an executable in a child process, optionally capturing its output.
    /// Arguments are passed as a list and never go through a shell, so they are not subject to shell interpretation.
    /// </summary>
    public class CommandLineRunner
    {
        /// <summary>
        /// Gets the output.
        /// </summary>
        public string Output { get; private set; }

        /// <summary>
        /// Gets the error.
        /// </summary>
        public string Error { get; private set; }

        /// <summary>
        /// Gets the exit code.
        /// </summary>
        public int ExitCode { get; private set; }

        /// <summary>
        /// Gets the commands.
        /// </summary>
        public string Commands { get; private set; }

        private readonly ProcessStartInfo processStartInfo;
        private int processId;

        /// <summary>
        /// Initializes a new instance of the <see cref="CommandLineRunner"/> class.
        /// </summary>
        /// <param name="workingDirectory">The working directory.</param>
        /// <param name="envVars">Extra environment variables for child process</param>
        /// <param name="redirectStreams">Should redirect stdin and stderr? default: true</param>
        /// <param name="fileName">The executable to run</param>
        public CommandLineRunner(string fileName, IDirectoryInfo workingDirectory = null, IDictionary<string, string> envVars = null, bool redirectStreams = true)
        {
            processStartInfo = new();
            processStartInfo.FileName = fileName;
            processStartInfo.WorkingDirectory = workingDirectory?.FullName ?? Environment.CurrentDirectory;
            processStartInfo.UseShellExecute = false;
            processStartInfo.RedirectStandardOutput = redirectStreams;
            processStartInfo.RedirectStandardError = redirectStreams;
            if (envVars != null)
            {
                foreach (var entry in envVars)
                {
                    processStartInfo.Environment.Add(entry.Key, entry.Value);
                }
            }
        }

        /// <summary>
        /// Runs the executable with the provided arguments
        /// </summary>
        /// <param name="arguments">The arguments, passed verbatim to the child process.</param>
        /// <param name="errorCodesToIgnore">error codes that should be ignored and not considered as errors</param>
        /// <param name="logOutputOnError">On error also throws the output</param>
        /// <param name="waitForExit">Waits for the process and validates the ExitCode</param>
        /// <param name="outputHandler">Handler for stdout output, used if logOutputOnError is false and redirecting streams</param>
        /// <param name="errorHandler">Handler for stderr output, used if logOutputOnError is false and redirecting streams</param>
        public void Run(IEnumerable<string> arguments, IEnumerable<int> errorCodesToIgnore = null, bool logOutputOnError = false, bool waitForExit = true, Action<string> outputHandler = null, Action<string> errorHandler = null)
        {
            processStartInfo.ArgumentList.Clear();
            foreach (var argument in arguments)
            {
                processStartInfo.ArgumentList.Add(argument);
            }
            var commands = string.Join(" ", processStartInfo.ArgumentList.Prepend(processStartInfo.FileName));
            Commands = commands;
            Output = string.Empty;
            Error = string.Empty;
            ExitCode = -1;

            Log.Debug($"Forking '{commands}'");

            outputHandler ??= Log.Debug;
            errorHandler ??= Log.Error;
            errorCodesToIgnore ??= [0];

            using var process = Process.Start(processStartInfo);
            if (process == null)
            {
                throw new Exception("Could not spawn child command");
            }
            this.processId = process.Id;
            process.ErrorDataReceived += (sender, args) => errorHandler(args.Data);
            process.OutputDataReceived += (sender, args) => outputHandler(args.Data);

            if (logOutputOnError)
            {
                string output = process.StandardOutput.ReadToEnd();
                if (!output.Equals("[]\n"))
                {
                    Output = output;
                }
                Log.Debug(Output);

                Error = process.StandardError.ReadToEnd();
            }
            else
            {
                if (processStartInfo.RedirectStandardOutput)
                {
                    process.BeginOutputReadLine();
                }
                if (processStartInfo.RedirectStandardError)
                {
                    process.BeginErrorReadLine();
                }
            }

            if (waitForExit)
            {
                // function that catch ctrl-c and exit
                Console.CancelKeyPress += (sender, eventArgs) =>
                {
                    eventArgs.Cancel = true;
                    Log.Debug("Caught SIGINT, terminating child process");
                    process.Disposed += (sender, args) => Log.Debug("Child process Disposed");
                    process.Kill(entireProcessTree: true);
                    Environment.Exit(-1);
                };
                process.WaitForExit();
                ExitCode = process.ExitCode;

                if (ExitCode != 0 && !errorCodesToIgnore.Contains(ExitCode))
                {
                    if (logOutputOnError)
                    {
                        Log.Error(Error);
                        Log.Verbose(Output);
                    }
                    Log.Warning("This is not an issue with cmf CLI, please check the log above for more details.");
                    Log.Error($"Exit code {ExitCode}.");

                    throw new CliException($"{commands} did not finish successfully.", (ErrorCode)ExitCode);
                }
            }
            Log.Debug("Exiting CommandLineRunner.Run");
        }

        public void KillProcess()
        {
            var process = Process.GetProcessById(processId);
            if (process != null)
            {
                process.Kill(true);
                process.WaitForExit();
            }
        }
    }
}
