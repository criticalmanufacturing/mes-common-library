using System.Collections.Concurrent;
using System.Globalization;
using System.Text;

namespace MESSimulator.Mes
{
    /// <summary>
    /// Durations of every MES call, per operation, for the end-of-run summary.
    /// </summary>
    public sealed class MesCallStatistics
    {
        private readonly ConcurrentDictionary<string, ConcurrentQueue<(double Milliseconds, bool Succeeded)>> _calls = new();

        public void Record(string operation, TimeSpan duration, bool succeeded)
        {
            _calls.GetOrAdd(operation, _ => new()).Enqueue((duration.TotalMilliseconds, succeeded));
        }

        public IReadOnlyList<MesCallSummary> Summarize()
        {
            return _calls
                .Select(entry =>
                {
                    var calls = entry.Value.ToArray();
                    var durations = calls.Select(c => c.Milliseconds).Order().ToArray();
                    return new MesCallSummary(
                        entry.Key,
                        calls.Length,
                        calls.Count(c => !c.Succeeded),
                        durations.Average(),
                        Percentile(durations, 0.95),
                        durations[^1],
                        durations.Sum());
                })
                .OrderByDescending(s => s.TotalMilliseconds)
                .ToList();
        }

        /// <summary>
        /// Plain-text table of <see cref="Summarize"/>, slowest operations (by total time) first.
        /// </summary>
        public string FormatSummary()
        {
            var summaries = Summarize();
            if (summaries.Count == 0)
            {
                return "No MES calls recorded.";
            }

            int nameWidth = Math.Max("Operation".Length, summaries.Max(s => s.Operation.Length));
            var text = new StringBuilder();
            text.AppendLine(CultureInfo.InvariantCulture,
                $"{"Operation".PadRight(nameWidth)}  {"Count",6}  {"Failed",6}  {"Avg ms",8}  {"P95 ms",8}  {"Max ms",8}  {"Total s",8}");
            foreach (var s in summaries)
            {
                text.AppendLine(CultureInfo.InvariantCulture,
                    $"{s.Operation.PadRight(nameWidth)}  {s.Count,6}  {s.Failures,6}  {s.AverageMilliseconds,8:F0}  {s.P95Milliseconds,8:F0}  {s.MaxMilliseconds,8:F0}  {s.TotalMilliseconds / 1000,8:F1}");
            }
            return text.ToString().TrimEnd();
        }

        private static double Percentile(double[] sorted, double percentile)
        {
            int index = (int)Math.Ceiling(percentile * sorted.Length) - 1;
            return sorted[Math.Clamp(index, 0, sorted.Length - 1)];
        }
    }

    public sealed record MesCallSummary(
        string Operation,
        int Count,
        int Failures,
        double AverageMilliseconds,
        double P95Milliseconds,
        double MaxMilliseconds,
        double TotalMilliseconds);
}
