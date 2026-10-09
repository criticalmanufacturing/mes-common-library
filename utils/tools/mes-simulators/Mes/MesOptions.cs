namespace MESSimulator.Mes
{
    /// <summary>
    /// How MES calls are retried ("Mes" configuration section).
    /// </summary>
    public sealed class MesOptions
    {
        public const string SectionName = "Mes";

        /// <summary>Retries after the first attempt on transient errors (stale object, SQL deadlock).</summary>
        public int RetryMaxAttempts { get; set; } = 5;

        /// <summary>Base delay of the linear backoff (1x, 2x, ...). Kept short: callers may hold the track lock.</summary>
        public TimeSpan RetryDelay { get; set; } = TimeSpan.FromSeconds(1);

        /// <summary>At most this many MES calls run at the same time (the others wait for a slot).</summary>
        public int MaxConcurrentCalls { get; set; } = 16;

        /// <summary>Why the options can't be used, or nothing.</summary>
        public IEnumerable<string> Errors()
        {
            if (RetryMaxAttempts is < 0 or > 10) yield return "Mes:RetryMaxAttempts must be between 0 and 10.";
            if (RetryDelay < TimeSpan.Zero || RetryDelay > TimeSpan.FromSeconds(30)) yield return "Mes:RetryDelay must be between 00:00:00 and 00:00:30.";
            if (MaxConcurrentCalls < 1) yield return "Mes:MaxConcurrentCalls must be >= 1.";
        }
    }

    /// <summary>
    /// Diagnostics switches ("Diagnostics" configuration section; --timings sets LogMesCallDurations).
    /// </summary>
    public sealed class DiagnosticsOptions
    {
        public const string SectionName = "Diagnostics";

        /// <summary>Log every MES call with its duration, outcome and number of attempts.</summary>
        public bool LogMesCallDurations { get; set; }

        /// <summary>Log a per-operation duration summary (count, failures, avg, p95, max) when the run ends.</summary>
        public bool SummaryOnExit { get; set; } = true;
    }
}
