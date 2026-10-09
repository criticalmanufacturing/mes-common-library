using System.Collections.Concurrent;

namespace MESSimulator.Steps
{
    /// <summary>
    /// One lock per resource around the calls that read and change its state (track-in/out, split, abort, durables),
    /// so two lots never change the same resource at the same time. Different resources run in parallel.
    /// </summary>
    public sealed class ResourceLocks
    {
        private readonly ConcurrentDictionary<string, SemaphoreSlim> _locks = new(StringComparer.OrdinalIgnoreCase);

        public async Task<IDisposable> AcquireAsync(string resourceName)
        {
            var semaphore = _locks.GetOrAdd(resourceName, _ => new SemaphoreSlim(1, 1));
            await semaphore.WaitAsync();
            return new Releaser(semaphore);
        }

        /// <summary>The same lock, for the synchronous startup code (e.g. aborting what previous runs left in process).</summary>
        public IDisposable Acquire(string resourceName)
        {
            var semaphore = _locks.GetOrAdd(resourceName, _ => new SemaphoreSlim(1, 1));
            semaphore.Wait();
            return new Releaser(semaphore);
        }

        private sealed class Releaser(SemaphoreSlim semaphore) : IDisposable
        {
            private int _released;

            public void Dispose()
            {
                if (Interlocked.Exchange(ref _released, 1) == 0)
                {
                    semaphore.Release();
                }
            }
        }
    }
}
