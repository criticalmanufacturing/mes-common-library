using Cmf.Navigo.BusinessObjects;
using Microsoft.Extensions.Logging;
using MESSimulator.Mes;

namespace MESSimulator.Steps.Actions
{
    /// <summary>
    /// Durables steps: before the track-in, makes the resource's durables match the lot's durables BOM (keeps the
    /// ones in use, detaches the others, attaches a queued durable for each missing BOM product).
    /// </summary>
    public sealed class DurablesAction(IMesGateway mes, ResourceLocks locks, IRandomSource random, ILogger<DurablesAction> logger) : IStepAction
    {
        public const string ActionKey = "durables";
        public const StepHook ActionHook = StepHook.BeforeTrackIn;

        public string Key => ActionKey;

        public StepHook Hook => ActionHook;

        // The queued durable picked for a mount can be taken by another resource's lot (the lock is per resource, and the
        // steppers share the reticles of a product): pick another one, a few times
        private const int maxPickAttempts = 4;

        public async Task ExecuteAsync(StepContext context)
        {
            using var _ = await locks.AcquireAsync(context.ResourceName);
            for (int attempt = 1; ; attempt++)
            {
                try
                {
                    ValidateDurables(context.Lot, context.ResourceName);
                    return;
                }
                catch (Exception ex) when (DurableTakenMeanwhile(ex.Message) && attempt < maxPickAttempts)
                {
                    logger.LogDebug($"A durable picked for '{context.ResourceName}' was taken meanwhile ({ex.Message}): picking another ({attempt}/{maxPickAttempts})");
                }
                catch (Exception ex) when (DurableChangeBlockedByLotsInProcess(ex.Message))
                {
                    // e.g. 200mm products on a grinder with 300mm wheels, while lots of the other size are in process: the
                    // wheels can be swapped once it is empty. The step waits and tries again (or another resource).
                    throw new ResourceUnsuitableException(
                        $"its durables can't be changed while other lots are in process ({ex.Message})", temporary: true);
                }
            }
        }

        /// <summary>
        /// "When changing the resource durables with materials in-process at the resource, the resource attached durable
        /// products and positions must remain unchanged."
        /// </summary>
        public static bool DurableChangeBlockedByLotsInProcess(string message) =>
            message.Contains("durables with materials in-process", StringComparison.OrdinalIgnoreCase)
            && message.Contains("must remain unchanged", StringComparison.OrdinalIgnoreCase);

        /// <summary>"The object RET L1 2EDN.015 is not Queued.": another lot attached the durable first.</summary>
        public static bool DurableTakenMeanwhile(string message) =>
            message.Contains("is not Queued", StringComparison.OrdinalIgnoreCase);

        /// <summary>The queued durables that are not already picked for this mount (the MES refuses a durable attached twice).</summary>
        public static List<Material> PickableDurables(IEnumerable<Material> queued, IEnumerable<long> alreadyPicked)
        {
            var picked = alreadyPicked.ToHashSet();
            return queued.Where(m => !picked.Contains(m.Id)).ToList();
        }

        private void ValidateDurables(Material material, string resourceName)
        {
            var resource = mes.Resources.GetByName(resourceName) ?? throw new InvalidOperationException($"Resource '{resourceName}' not found");
            var durablesData = mes.Resources.GetDurablesData(material, resource);

            var currentDurables = durablesData.Durables;
            var durablesToDetach = new MaterialCollection();
            var durablesToKeepUsing = new MaterialCollection();
            var durablesToAttach = new ResourceDurableCollection();

            // A BOM product already covered by an attached durable keeps it; otherwise attach a queued one
            foreach (var bomProduct in durablesData.DurablesBOM.First().BomProducts)
            {
                var alreadyHasDurable = false;
                foreach (var durable in currentDurables)
                {
                    if (bomProduct.TargetEntity.Name == durable.Product.Name)
                    {
                        alreadyHasDurable = true;
                        durablesToKeepUsing.Add(durable);
                    }
                }
                if (!alreadyHasDurable)
                {
                    // A BOM can list a product more than once (two grinding wheels): each line needs another durable
                    var candidates = PickableDurables(
                        mes.Materials.FindByProduct(bomProduct.TargetEntity.Id, MaterialSystemState.Queued),
                        durablesToAttach.Select(a => a.TargetEntity.Id));
                    if (candidates.Count == 0)
                    {
                        // e.g. the only reticle of the layer is mounted on another stepper: that stepper can run the lot instead
                        throw new ResourceUnsuitableException($"no queued '{bomProduct.TargetEntity.Name}' durable to mount");
                    }
                    durablesToAttach.Add(new ResourceDurable()
                    {
                        Position = durablesToAttach.Count + 1,
                        TargetEntity = candidates[random.Next(0, candidates.Count)],
                    });
                }
            }

            durablesToDetach.AddRange(currentDurables.Except(durablesToKeepUsing));

            if (durablesToDetach.Count > 0 || durablesToAttach.Count > 0)
            {
                logger.LogDebug($"Durables on '{resourceName}' for {material.Name}: detaching {durablesToDetach.Count}, attaching {durablesToAttach.Count}");
                mes.Resources.ManageDurables(material, resource,
                    durablesToDetach: durablesToDetach.Count > 0 ? durablesToDetach : null,
                    durablesToAttach: durablesToAttach.Count > 0 ? durablesToAttach : null);
            }
        }
    }
}
