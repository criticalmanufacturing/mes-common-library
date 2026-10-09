using Cmf.Navigo.BusinessObjects;
using Microsoft.Extensions.Logging;
using MESSimulator.Mes;

namespace MESSimulator.Steps.Actions
{
    /// <summary>
    /// SILICON WAFER COMPOSE: after the track-in, composes the lot from the wafers it was created with.
    /// </summary>
    public sealed class ComposeAction(IMesGateway mes, IMaterialTracker tracker, ILogger<ComposeAction> logger) : IStepAction
    {
        // Dies per wafer when a wafer has no secondary quantity
        private const int defaultDiesPerWafer = 500;

        public const string ActionKey = "compose";
        public const StepHook ActionHook = StepHook.AfterTrackIn;

        public string Key => ActionKey;

        public StepHook Hook => ActionHook;

        public Task ExecuteAsync(StepContext context)
        {
            var wafers = context.Data.Wafers
                ?? throw new InvalidOperationException($"'{context.Step.Name}' needs the lot's wafers to compose '{context.Lot.Name}'");

            logger.LogDebug($"Composing Lot '{context.Lot.Name}' with {wafers.Count} wafer material(s)");

            var sourceMaterials = new ComposeSourceMaterialCollection();
            foreach (Material wafer in wafers)
            {
                sourceMaterials.Add(new ComposeSourceMaterial()
                {
                    Material = tracker.Reload(wafer),
                    Position = sourceMaterials.Count + 1,
                    ComposedPrimaryQuantity = wafer.PrimaryQuantity ?? 1,
                    ComposedSecondaryQuantity = wafer.SecondaryQuantity ?? defaultDiesPerWafer
                });
            }

            context.Lot = mes.Materials.Compose(tracker.Reload(context.Lot), sourceMaterials);

            logger.LogInformation($"Composed Lot '{context.Lot.Name}'");
            return Task.CompletedTask;
        }
    }

    /// <summary>
    /// Sub-material track-in (e.g. SILICON WAFER PEELING): after the lot's track-in, tracks its wafers in and out.
    /// </summary>
    public sealed class SubMaterialTrackingAction(IMesGateway mes, IMaterialTracker tracker) : IStepAction
    {
        public const string ActionKey = "subMaterialTracking";
        public const StepHook ActionHook = StepHook.AfterTrackIn;

        public string Key => ActionKey;

        public StepHook Hook => ActionHook;

        public async Task ExecuteAsync(StepContext context)
        {
            context.Lot = mes.Materials.LoadChildren(context.Lot);
            context.Lot.SubMaterials = await tracker.TrackInSubMaterialsAsync(context.Lot.SubMaterials, context.ResourceName);
            await tracker.TrackOutSubMaterialsAsync(context.Lot.SubMaterials, context.ResourceName);
        }
    }

    /// <summary>
    /// Split &amp; track-out (ASHING PRE): instead of a normal track-out, splits the lot's wafers into random groups and
    /// tracks each group out as a new lot (the parent terminates with the last group), then moves them all next.
    /// Resumable: the lots already split off are kept in the lot's run data, so running it again (after a failure half-way)
    /// only splits the wafers left and still hands every new lot on.
    /// </summary>
    public sealed class SplitTrackOutAction(IMesGateway mes, IMaterialTracker tracker, IRandomSource random, ILogger<SplitTrackOutAction> logger) : IStepAction
    {
        // At most this many lots come out of one split
        private const int maxSplitGroups = 4;

        public const string ActionKey = "splitTrackOut";
        public const StepHook ActionHook = StepHook.TrackOut;

        public string Key => ActionKey;

        public StepHook Hook => ActionHook;

        public bool Resumable => true;

        public async Task ExecuteAsync(StepContext context)
        {
            // Lots already split off by an earlier attempt that failed half-way
            var childLots = context.Data.SplitChildren;
            var lot = SplitChildren.ReloadParent(tracker, context.Lot);
            var wafers = lot == null ? [] : mes.Materials.LoadChildren(lot).SubMaterials.Cast<Material>().ToList();
            if (wafers.Count == 0 && childLots.Count == 0)
            {
                throw new InvalidOperationException($"'{context.Lot.Name}' has no wafers to split at {context.Step.Name}");
            }

            if (wafers.Count > 0)
            {
                var groups = SplitIntoRandomGroups(wafers, maxSplitGroups, random);
                logger.LogDebug($"Split & TrackOut Lot '{lot!.Name}' with {wafers.Count} wafer(s) into {groups.Count} lot(s){(childLots.Count > 0 ? $", after the {childLots.Count} split off before" : "")}");

                // The step only allows Split & TrackOut: every group becomes a new lot and the parent terminates once its
                // last wafers are split off
                for (int i = 0; i < groups.Count; i++)
                {
                    var childLot = await tracker.SplitAndTrackOutAsync(lot, groups[i], isLastSplit: i == groups.Count - 1);
                    childLots.Add(childLot);
                    logger.LogInformation($"Split & Tracked Out Lot '{childLot.Name}' with {groups[i].Count} wafer(s) from '{lot.Name}'");
                }
            }

            context.OutputLots.Clear();
            context.OutputLots.AddRange(SplitChildren.MoveOn(tracker, childLots, logger));
            childLots.Clear();
        }

        /// <summary>
        /// Shuffles <paramref name="items"/> and cuts them into 1..<paramref name="maxGroups"/> non-empty groups of random size.
        /// </summary>
        public static List<List<T>> SplitIntoRandomGroups<T>(IReadOnlyList<T> items, int maxGroups, IRandomSource random)
        {
            if (items.Count == 0)
            {
                return [];
            }

            var shuffled = items.OrderBy(_ => random.Next(0, int.MaxValue)).ToList();

            int groupCount = random.Next(1, Math.Min(maxGroups, shuffled.Count) + 1);

            // Pick groupCount - 1 distinct cut points between items
            var cuts = Enumerable.Range(1, shuffled.Count - 1)
                .OrderBy(_ => random.Next(0, int.MaxValue))
                .Take(groupCount - 1)
                .Order()
                .Append(shuffled.Count)
                .ToList();

            var groups = new List<List<T>>();
            int start = 0;
            foreach (var cut in cuts)
            {
                groups.Add(shuffled.GetRange(start, cut - start));
                start = cut;
            }

            return groups;
        }
    }

    /// <summary>
    /// Shared by the split track-outs: finding the parent again on a re-run, and moving the new lots on.
    /// </summary>
    internal static class SplitChildren
    {
        /// <summary>The parent, or null once it is gone (terminated with its last split).</summary>
        public static Material? ReloadParent(IMaterialTracker tracker, Material lot)
        {
            try
            {
                var parent = tracker.Reload(lot);
                return parent.UniversalState == Cmf.Foundation.Common.Base.UniversalState.Terminated ? null : parent;
            }
            catch (InvalidOperationException)
            {
                return null;
            }
        }

        /// <summary>
        /// Moves the new lots next (together when they can go together, else one by one). A lot that can't be moved is
        /// still handed on: the lot flow moves a processed lot on before anything else.
        /// </summary>
        public static List<Material> MoveOn(IMaterialTracker tracker, List<Material> childLots, ILogger logger, Func<List<Material>, List<Material>?>? moveNext = null)
        {
            moveNext ??= tracker.MoveNext;
            try
            {
                return moveNext(childLots) ?? [.. childLots];
            }
            catch (Exception ex)
            {
                logger.LogWarning($"Moving {childLots.Count} split lot(s) on failed ({ex.Message}): moving them one by one");
            }

            var moved = new List<Material>();
            foreach (var child in childLots)
            {
                try
                {
                    moved.AddRange(moveNext([child]) ?? [child]);
                }
                catch (Exception ex)
                {
                    logger.LogWarning($"Split lot '{child.Name}' could not be moved on ({ex.Message}): its flow moves it on");
                    moved.Add(child);
                }
            }
            return moved;
        }
    }

    /// <summary>
    /// INSP CD runs for lots of the "2EDN Product Family" product group, or lots with the INSPCD attribute set to TRUE.
    /// </summary>
    public sealed class InspCdRequiredCondition(IMesGateway mes) : IStepCondition
    {
        private const string inspectedProductGroup = "2EDN Product Family";
        private const string inspCdAttribute = "INSPCD";

        public const string ConditionKey = "inspCdRequired";

        public string Key => ConditionKey;

        public bool ShouldRun(Material lot)
        {
            var product = mes.MasterData.GetById<Product>(lot.Product.Id, levelsToLoad: 2);

            // Attributes is Dictionary<string, object>: compare the value as a string, and a missing attribute means no
            bool attributeSet = lot.Attributes != null
                && lot.Attributes.TryGetValue(inspCdAttribute, out var value)
                && string.Equals(value?.ToString(), "TRUE", StringComparison.OrdinalIgnoreCase);

            return product?.ProductGroup?.Name == inspectedProductGroup || attributeSet;
        }
    }
}
