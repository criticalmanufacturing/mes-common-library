using Microsoft.Extensions.Options;

namespace MESSimulator.Steps
{
    /// <summary>
    /// Random numbers for the simulation; injectable so tests can make runs deterministic.
    /// </summary>
    public interface IRandomSource
    {
        /// <summary>Random integer in [<paramref name="minInclusive"/>, <paramref name="maxExclusive"/>).</summary>
        int Next(int minInclusive, int maxExclusive);
    }

    public sealed class RandomSource(Random random) : IRandomSource
    {
        private readonly object _lock = new();

        public int Next(int minInclusive, int maxExclusive)
        {
            // Random.Shared is thread-safe; a seeded Random (tests) is not
            lock (_lock)
            {
                return random.Next(minInclusive, maxExclusive);
            }
        }
    }

    /// <summary>
    /// Turns simulated seconds into real time, applying --speed.
    /// </summary>
    public sealed class SimulationClock(IOptions<SimulationOptions> options)
    {
        private readonly decimal _speed = options.Value.Speed;

        /// <summary>Shortest real time between two checks of something only the MES can tell (Simulation:MinPollInterval).</summary>
        private readonly TimeSpan _minPollInterval = options.Value.MinPollInterval;

        public TimeSpan Scale(int seconds) => TimeSpan.FromMilliseconds(Decimal.ToDouble(seconds * 1000m / _speed));

        /// <summary>
        /// A polling wait: <paramref name="seconds"/> simulated, but never under Simulation:MinPollInterval real time,
        /// so a high speed (e.g. 3600: 1 h in 1 s) doesn't flood the MES with checks.
        /// </summary>
        public TimeSpan PollInterval(int seconds)
        {
            var scaled = Scale(seconds);
            return scaled < _minPollInterval ? _minPollInterval : scaled;
        }

        /// <summary>
        /// How many polls of <paramref name="intervalSeconds"/> cover <paramref name="budgetSeconds"/> simulated, and at
        /// least <paramref name="minRealBudget"/> real time (at a high speed the MES calls, not the simulated times, set the pace).
        /// </summary>
        public int PollCount(int budgetSeconds, int intervalSeconds, TimeSpan minRealBudget)
        {
            var interval = PollInterval(intervalSeconds);
            var budget = Scale(budgetSeconds);
            if (budget < minRealBudget)
            {
                budget = minRealBudget;
            }
            return interval <= TimeSpan.Zero ? Math.Max(1, budgetSeconds) : Math.Max(1, (int)Math.Ceiling(budget / interval));
        }

        /// <summary>Random duration within [<paramref name="minSeconds"/>, <paramref name="maxSeconds"/>) simulated seconds, scaled.</summary>
        public TimeSpan RandomDuration(IRandomSource random, int minSeconds, int maxSeconds) =>
            Scale(maxSeconds > minSeconds ? random.Next(minSeconds, maxSeconds) : minSeconds);

        /// <summary>A lot's process time at a step, scaled: the per-lot time plus the per-wafer time for each of its wafers.</summary>
        public TimeSpan ProcessTime(IRandomSource random, StepDefinition step, int wafers) =>
            RandomDuration(random, step.MinSeconds, step.MaxSeconds)
            + RandomDuration(random, step.MinSecondsPerWafer, step.MaxSecondsPerWafer) * Math.Max(0, wafers);
    }
}
