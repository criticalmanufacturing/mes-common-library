using Cmf.Navigo.BusinessObjects;

namespace MESSimulator.Pipeline
{
    /// <summary>
    /// Decides which queued lots go into the next batch (no MES calls).
    /// </summary>
    public static class BatchPlanner
    {
        /// <summary>A lot's size for batching: its primary quantity, or its sub-materials' when it has none.</summary>
        public static int LotQuantity(Material lot) =>
            lot.PrimaryQuantity is decimal quantity and > 0 ? (int)quantity : (int)(lot.SubMaterialsPrimaryQuantity ?? 0);

        /// <summary>
        /// One batch from the lots, oldest first (by DateEnteredStep): lots are added while the total stays within
        /// <paramref name="maxQuantity"/>; once a lot does not fit, it and every later lot wait, so arrival order is
        /// kept. The batch runs only when it reaches <paramref name="minQuantity"/>; otherwise every lot waits.
        /// A first lot above <paramref name="maxQuantity"/> runs on its own so it never gets stuck.
        /// </summary>
        public static BatchPlan Plan(IEnumerable<Material> lots, int minQuantity, int maxQuantity)
        {
            var ordered = lots.OrderBy(m => m.DateEnteredStep ?? DateTime.MaxValue).ToList();

            var batchLots = new List<Material>();
            var leftovers = new List<Material>();
            int total = 0;

            foreach (var lot in ordered)
            {
                int quantity = LotQuantity(lot);
                bool oversizedFirstLot = batchLots.Count == 0 && quantity > maxQuantity;

                if (leftovers.Count == 0 && (total + quantity <= maxQuantity || oversizedFirstLot))
                {
                    batchLots.Add(lot);
                    total += quantity;
                }
                else
                {
                    leftovers.Add(lot);
                }
            }

            return batchLots.Count == 0 || total < minQuantity
                ? new BatchPlan([], ordered, 0)
                : new BatchPlan(batchLots, leftovers, total);
        }
    }

    /// <param name="BatchLots">Lots of the batch to run now; empty when the minimum is not reached.</param>
    /// <param name="Leftovers">Lots that wait for the next batch.</param>
    /// <param name="Quantity">Total quantity of <paramref name="BatchLots"/>.</param>
    public sealed record BatchPlan(IReadOnlyList<Material> BatchLots, IReadOnlyList<Material> Leftovers, int Quantity)
    {
        public bool HasBatch => BatchLots.Count > 0;
    }
}
