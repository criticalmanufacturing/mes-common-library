using System.Collections.Concurrent;

namespace MESSimulator.Steps
{
    /// <summary>
    /// The lots whose resource setup (feeders) is done and that have not tracked in yet. A shared feeder can only be swapped
    /// when nothing is in process on the resource, but such a lot is not in process yet: without this, another lot swaps the
    /// feeder in that window and the track-in finds the wrong product ("no Material of Product AZ Spray found in the
    /// Consumable Feeds"). The setup registers the lot, the executor releases it after the track-in.
    /// </summary>
    public sealed class SetUpLots
    {
        private readonly object _lock = new();
        private readonly Dictionary<string, HashSet<string>> _byResource = new(StringComparer.OrdinalIgnoreCase);

        public void Add(string resourceName, string lotName)
        {
            lock (_lock)
            {
                if (!_byResource.TryGetValue(resourceName, out var lots))
                {
                    _byResource[resourceName] = lots = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                }
                lots.Add(lotName);
            }
        }

        /// <summary>The lot has tracked in (or failed to): it no longer holds its setup on any resource.</summary>
        public void Release(string lotName)
        {
            lock (_lock)
            {
                foreach (var lots in _byResource.Values)
                {
                    lots.Remove(lotName);
                }
            }
        }

        /// <summary>How many lots other than <paramref name="lotName"/> are set up on the resource and waiting to track in.</summary>
        public int CountOthers(string resourceName, string lotName)
        {
            lock (_lock)
            {
                return _byResource.TryGetValue(resourceName, out var lots) ? lots.Count(l => !l.Equals(lotName, StringComparison.OrdinalIgnoreCase)) : 0;
            }
        }
    }
}
