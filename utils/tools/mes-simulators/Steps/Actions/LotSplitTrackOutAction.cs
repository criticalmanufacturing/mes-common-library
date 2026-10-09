using Cmf.Navigo.BusinessObjects;
using Microsoft.Extensions.Logging;
using MESSimulator.Mes;

namespace MESSimulator.Steps.Actions
{
    /// <summary>
    /// Split & track-out steps for lots without sub-materials (e.g. COMPRESSOR TUB BRAZE, which only allows a split at
    /// track-out): the lot's quantity is cut into 1..4 random parts, each split off into a new lot and tracked out; the
    /// parent terminates when its last part goes. Unlike <c>splitTrackOut</c>, which splits by wafers. Resumable like it:
    /// a re-run splits only the quantity left and keeps the lots split off before.
    /// </summary>
    public sealed class LotSplitTrackOutAction(IMaterialTracker tracker, IRandomSource random, ILogger<LotSplitTrackOutAction> logger) : IStepAction
    {
        // At most this many lots come out of one split
        private const int maxSplitGroups = 4;

        public const string ActionKey = "lotSplitTrackOut";
        public const StepHook ActionHook = StepHook.TrackOut;

        public string Key => ActionKey;

        public StepHook Hook => ActionHook;

        public bool Resumable => true;

        /// <summary>The quantity cut into 1..<paramref name="maxGroups"/> random whole parts (each at least 1).</summary>
        public static List<int> SplitQuantities(int quantity, int maxGroups, IRandomSource random) =>
            SplitTrackOutAction.SplitIntoRandomGroups(Enumerable.Range(0, quantity).ToList(), maxGroups, random).Select(g => g.Count).ToList();

        // Many lots split on the same resource at once (COMPRESSOR TUB BRAZE): the resource keeps changing under each
        // call, past the tracker's own retries. Wait a moment and try again, a few times.
        private const int splitAttempts = 5;

        private async Task<Material> SplitWithRetriesAsync(Material lot, int quantity, bool isLastSplit)
        {
            for (int attempt = 1; ; attempt++)
            {
                try
                {
                    return await tracker.SplitQuantityAndTrackOutAsync(lot, quantity, isLastSplit);
                }
                catch (Exception ex) when (attempt < splitAttempts && ex.Message.Contains("has changed since last viewed", StringComparison.OrdinalIgnoreCase))
                {
                    logger.LogDebug($"Splitting {quantity} off '{lot.Name}' hit a changed resource, retrying ({attempt}/{splitAttempts}): {ex.Message}");
                    await Task.Delay(TimeSpan.FromMilliseconds(500 * attempt));
                }
            }
        }

        // Moving the split lots on also checks the resource they were processed on, which other lots keep changing. Null
        // when it still fails: the lots are then moved one by one (SplitChildren.MoveOn)
        private async Task<List<Material>?> MoveNextWithRetriesAsync(List<Material> lots)
        {
            for (int attempt = 1; ; attempt++)
            {
                try
                {
                    return tracker.MoveNext(tracker.Reload(lots).Cast<Material>().ToList()) ?? [.. lots];
                }
                catch (Exception ex) when (attempt < splitAttempts && ex.Message.Contains("has changed since last viewed", StringComparison.OrdinalIgnoreCase))
                {
                    logger.LogDebug($"Moving {lots.Count} split lot(s) on hit a changed resource, retrying ({attempt}/{splitAttempts}): {ex.Message}");
                    await Task.Delay(TimeSpan.FromMilliseconds(500 * attempt));
                }
                catch (Exception ex)
                {
                    logger.LogWarning($"Moving {lots.Count} split lot(s) on failed: {ex.Message}");
                    return null;
                }
            }
        }

        public async Task ExecuteAsync(StepContext context)
        {
            // Lots already split off by an earlier attempt that failed half-way
            var childLots = context.Data.SplitChildren;
            var lot = SplitChildren.ReloadParent(tracker, context.Lot);
            var parts = lot == null ? [] : SplitQuantities(decimal.ToInt32(lot.PrimaryQuantity ?? 0), maxSplitGroups, random);
            if (parts.Count == 0 && childLots.Count == 0)
            {
                throw new InvalidOperationException($"'{context.Lot.Name}' has no quantity to split at {context.Step.Name}");
            }

            for (int i = 0; i < parts.Count; i++)
            {
                var childLot = await SplitWithRetriesAsync(lot!, parts[i], isLastSplit: i == parts.Count - 1);
                childLots.Add(childLot);
                logger.LogInformation($"Split & Tracked Out Lot '{childLot.Name}' with {parts[i]} unit(s) from '{lot!.Name}'");
            }

            context.OutputLots.Clear();
            var moved = await MoveNextWithRetriesAsync(childLots);
            context.OutputLots.AddRange(moved ?? SplitChildren.MoveOn(tracker, childLots, logger));
            childLots.Clear();
        }
    }
}
