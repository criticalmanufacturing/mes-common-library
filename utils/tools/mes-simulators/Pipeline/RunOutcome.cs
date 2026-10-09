namespace MESSimulator.Pipeline
{
    /// <summary>
    /// How the simulation ended, for the process exit code: the host logs a failed background service and stops, but
    /// would otherwise exit with 0.
    /// </summary>
    public sealed class RunOutcome
    {
        private Exception? _failure;

        /// <summary>The first failure that stopped the run, or null.</summary>
        public Exception? Failure => Volatile.Read(ref _failure);

        public bool Failed => Failure != null;

        public void Fail(Exception failure) => Interlocked.CompareExchange(ref _failure, failure, null);
    }
}
