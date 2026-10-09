using Microsoft.Extensions.Configuration;

namespace MESSimulator
{
    /// <summary>
    /// Where the simulator's configuration comes from, so one build can run against several environments.
    /// </summary>
    public static class SimulatorConfiguration
    {
        /// <summary>Prefix of the environment variables that override the configuration (stripped from the key).</summary>
        public const string EnvironmentPrefix = "MESSIM_";

        /// <summary>Environment variable with the line file, when --line is not given.</summary>
        public const string LineFileVariable = EnvironmentPrefix + "LINE_FILE";

        /// <summary>The line files shipped with the simulator (simulationConfiguration.semi.json, ...).</summary>
        public const string LineFilePattern = "simulationConfiguration*.json";

        /// <summary>The line file to use: the --line argument, else the MESSIM_LINE_FILE variable; null when neither is set.</summary>
        public static string? ResolveLineFile(string? lineFile) =>
            string.IsNullOrWhiteSpace(lineFile) ? NullIfBlank(Environment.GetEnvironmentVariable(LineFileVariable)) : lineFile;

        /// <summary>Why the simulator can't start without a line file, with the line files it can find to choose from.</summary>
        public static string MissingLineFileMessage(params string[] directories)
        {
            var candidates = directories.Where(Directory.Exists)
                .SelectMany(d => Directory.GetFiles(d, LineFilePattern))
                .Select(Path.GetFileName)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Order(StringComparer.OrdinalIgnoreCase)
                .ToList();
            return "No line file: pass --line <file> (or set " + LineFileVariable + ")." + (candidates.Count > 0
                ? $" Line files found: {string.Join(", ", candidates)} (e.g. --line {candidates[0]})."
                : "");
        }

        private static string? NullIfBlank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value;

        /// <summary>
        /// Adds the sources, from lowest to highest precedence:
        /// <list type="number">
        /// <item>appsettings.json (optional: the environment variables can supply everything);</item>
        /// <item>the line file: <paramref name="lineFile"/>, else the MESSIM_LINE_FILE variable (one of them is required);</item>
        /// <item>environment variables with the MESSIM_ prefix, "__" separating the levels
        /// (MESSIM_ClientConfiguration__Connection__EnvironmentAddress, MESSIM_Line__Order__MaxOrders,
        /// MESSIM_Line__Chaos__ScrapReasons__0);</item>
        /// <item><paramref name="overrides"/>, i.e. the command line.</item>
        /// </list>
        /// </summary>
        /// <param name="baseDirectory">Where appsettings.json is.</param>
        /// <param name="lineFile">The --line argument; a relative path is resolved against the current directory, then
        /// against <paramref name="baseDirectory"/> (where the shipped line files are copied).</param>
        /// <exception cref="InvalidOperationException">Neither <paramref name="lineFile"/> nor MESSIM_LINE_FILE is set.</exception>
        public static IConfigurationBuilder AddSimulatorSources(this IConfigurationBuilder builder, string baseDirectory,
            string? lineFile, IEnumerable<KeyValuePair<string, string?>> overrides)
        {
            lineFile = ResolveLineFile(lineFile)
                ?? throw new InvalidOperationException(MissingLineFileMessage(Directory.GetCurrentDirectory(), baseDirectory));

            builder
                .SetBasePath(baseDirectory)
                .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
                .AddJsonFile(LineFilePath(lineFile, baseDirectory), optional: false, reloadOnChange: false);

            return builder
                .AddEnvironmentVariables(EnvironmentPrefix)
                .AddInMemoryCollection(overrides);
        }

        // "--line simulationConfiguration.semi.json" works from the source folder and from the output folder
        private static string LineFilePath(string lineFile, string baseDirectory)
        {
            var fromCurrentDirectory = Path.GetFullPath(lineFile);
            if (File.Exists(fromCurrentDirectory) || Path.IsPathRooted(lineFile))
            {
                return fromCurrentDirectory;
            }
            var fromBaseDirectory = Path.GetFullPath(lineFile, baseDirectory);
            return File.Exists(fromBaseDirectory) ? fromBaseDirectory : fromCurrentDirectory;
        }
    }
}
