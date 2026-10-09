using System.Collections.Concurrent;

namespace MESSimulator.Pipeline
{
    /// <summary>
    /// Lot runs started in the background, so shutdown can wait for them to finish.
    /// </summary>
    public sealed class InFlightLots
    {
        private readonly ConcurrentDictionary<Task, byte> _runs = new();

        public void Track(Task run)
        {
            _runs.TryAdd(run, 0);
            run.ContinueWith(completed => _runs.TryRemove(completed, out _), TaskScheduler.Default);
        }

        public int Count => _runs.Count;

        /// <summary>Waits until every tracked run (including ones started meanwhile) has finished.</summary>
        public async Task WhenAllAsync()
        {
            while (!_runs.IsEmpty)
            {
                await Task.WhenAll(_runs.Keys);
            }
        }
    }
}
