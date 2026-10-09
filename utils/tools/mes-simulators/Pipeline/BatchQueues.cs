using Cmf.Navigo.BusinessObjects;
using MESSimulator.Line;

namespace MESSimulator.Pipeline
{
    /// <summary>
    /// The lots waiting at each batch step.
    /// </summary>
    public sealed class BatchQueues
    {
        private readonly Dictionary<string, List<Material>> _queues = new(StringComparer.OrdinalIgnoreCase);

        public BatchQueues(LineDefinition line)
        {
            foreach (var stepName in line.Batches.Keys)
            {
                _queues[stepName] = [];
            }
        }

        /// <summary>Lots waiting at every batch step (their lot runs have ended until the batch hands them back).</summary>
        public int WaitingCount
        {
            get
            {
                int count = 0;
                foreach (var queue in _queues.Values)
                {
                    lock (queue)
                    {
                        count += queue.Count;
                    }
                }
                return count;
            }
        }

        public int Enqueue(string stepName, Material lot)
        {
            var queue = Get(stepName);
            lock (queue)
            {
                queue.Add(lot);
                return queue.Count;
            }
        }

        public void EnqueueRange(string stepName, IEnumerable<Material> lots)
        {
            var queue = Get(stepName);
            lock (queue)
            {
                queue.AddRange(lots);
            }
        }

        /// <summary>Total quantity and number of lots waiting.</summary>
        public (int Lots, int Quantity) Peek(string stepName)
        {
            var queue = Get(stepName);
            lock (queue)
            {
                return (queue.Count, queue.Sum(BatchPlanner.LotQuantity));
            }
        }

        /// <summary>Takes every waiting lot; the caller puts back the ones that do not make it into the batch.</summary>
        public List<Material> TakeAll(string stepName)
        {
            var queue = Get(stepName);
            lock (queue)
            {
                var lots = queue.ToList();
                queue.Clear();
                return lots;
            }
        }

        private List<Material> Get(string stepName) =>
            _queues.TryGetValue(stepName, out var queue)
                ? queue
                : throw new InvalidOperationException($"'{stepName}' is not a batch step in simulationConfiguration.json");
    }
}
