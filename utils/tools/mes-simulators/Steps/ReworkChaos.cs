using System.Collections.Concurrent;
using Cmf.Foundation.Common.Base;
using Cmf.Navigo.BusinessObjects;
using Microsoft.Extensions.Logging;
using MESSimulator.Line;
using MESSimulator.Mes;

namespace MESSimulator.Steps
{
    /// <summary>
    /// Chaos on the line: sends some lots to rework and scraps some of their wafers.
    /// </summary>
    public interface IReworkChaos
    {
        /// <summary>
        /// Maybe sends the lot, queued at <paramref name="stepName"/>, to one of the rework flows the MES offers there.
        /// Returns the lot as it is now: unchanged (no rework), in its rework flow, or null when all its wafers were
        /// scrapped (the lot is gone).
        /// </summary>
        Material? MaybeRework(Material lot, string stepName, LotRunData data);
    }

    /// <summary>
    /// A lot queued at a main-flow step goes, with its flow's chance in <see cref="ChaosOptions.ReworkFlows"/>, to a
    /// rework path the MES offers for it there (e.g. "LITHO PHOTO Nok OK" from FUSE Expose to RWK_FUSE Photoresist),
    /// at most <see cref="ChaosOptions.MaxReworksPerLot"/> times. A reworked lot then loses, with
    /// <see cref="ChaosOptions.ScrapProbability"/>, some of its wafers (up to <see cref="ChaosOptions.MaxScrapFraction"/>)
    /// for one of <see cref="ChaosOptions.ScrapReasons"/>. Once its rework flow is done, the MES returns it to the main flow.
    /// </summary>
    public sealed class ReworkChaos(IMesGateway mes, IMaterialTracker tracker, LineDefinition line, IRandomSource random, ILogger<ReworkChaos> logger) : IReworkChaos
    {
        // Rework flows are named RWK_... in this MES
        private const string reworkFlowPrefix = "RWK_";

        private readonly ConcurrentDictionary<string, Reason> _reasons = new(StringComparer.OrdinalIgnoreCase);

        // Loss reasons each step accepts (a scrap is recorded at the lot's current step)
        private readonly ConcurrentDictionary<long, HashSet<string>> _stepLossReasons = new();

        // Reworks per lot name for the whole run: a lot's run data is new after a batch, the count must survive it
        private readonly ConcurrentDictionary<string, int> _reworks = new(StringComparer.OrdinalIgnoreCase);

        private ChaosOptions Chaos => line.Chaos;

        public Material? MaybeRework(Material lot, string stepName, LotRunData data)
        {
            if (Chaos.ReworkFlows.Count == 0
                || Math.Max(data.Reworks, _reworks.GetValueOrDefault(lot.Name)) >= Chaos.MaxReworksPerLot
                || lot.FlowPath?.StartsWith(reworkFlowPrefix, StringComparison.OrdinalIgnoreCase) == true)
            {
                return lot;
            }

            // One roll per flow, before asking the MES (most lots at most steps go on without a single extra call);
            // the paths offered here into a flow that rolled a hit are the candidates
            var hits = Chaos.ReworkFlows.Where(f => Roll(f.Value)).Select(f => f.Key).ToHashSet(StringComparer.OrdinalIgnoreCase);
            if (hits.Count == 0)
            {
                return lot;
            }

            List<ReworkPath> paths;
            try
            {
                paths = mes.Materials.GetPossibleReworkPaths(lot);
            }
            catch (Exception ex)
            {
                logger.LogDebug($"No rework paths for '{lot.Name}' at {stepName}: {ex.Message}");
                return lot;
            }
            paths = paths.Where(p => hits.Contains(FlowName(p.GotoFlowPath))).ToList();
            if (paths.Count == 0)
            {
                return lot;
            }

            var path = paths[random.Next(0, paths.Count)];
            try
            {
                lot = mes.Materials.Rework(tracker.Reload(lot), path);
            }
            catch (Exception ex)
            {
                // e.g. the rework reason's limit was reached: the lot just carries on
                logger.LogWarning($"Rework of '{lot.Name}' at {stepName} ({path.ReworkReason?.Name}) refused: {ex.Message}");
                return tracker.Reload(lot);
            }

            data.Reworks++;
            _reworks.AddOrUpdate(lot.Name, 1, (_, count) => count + 1);
            logger.LogInformation($"Rework: '{lot.Name}' sent from {stepName} to {path.GotoFlowPath} ({path.ReworkReason?.Name}), returns to {path.ReturnFlowPath}");

            return Roll(Chaos.ScrapProbability) ? ScrapSomeWafers(lot) : tracker.Reload(lot);
        }

        /// <summary>
        /// How many of <paramref name="waferCount"/> wafers to scrap: between 1 and <paramref name="maxFraction"/> of them.
        /// </summary>
        public static int WafersToScrap(int waferCount, decimal maxFraction, IRandomSource random)
        {
            if (waferCount <= 0)
            {
                return 0;
            }
            int max = Math.Max(1, (int)Math.Floor(waferCount * maxFraction));
            return random.Next(1, max + 1);
        }

        private Material? ScrapSomeWafers(Material lot)
        {
            lot = mes.Materials.LoadChildren(tracker.Reload(lot));
            var wafers = lot.SubMaterials?.Cast<Material>().Where(w => w.UniversalState != UniversalState.Terminated).ToList() ?? [];
            int count = WafersToScrap(wafers.Count, Chaos.MaxScrapFraction, random);
            if (count == 0)
            {
                return lot;
            }

            var scrapped = new List<Material>();
            while (scrapped.Count < count)
            {
                var wafer = wafers[random.Next(0, wafers.Count)];
                wafers.Remove(wafer);
                scrapped.Add(wafer);
            }

            // Only reasons the step accepts (e.g. RwPreClean only takes Scratch-Loss)
            var allowed = lot.Step == null ? null : _stepLossReasons.GetOrAdd(lot.Step.Id, LoadStepLossReasons);
            var candidates = Chaos.ScrapReasons.Where(r => allowed == null || allowed.Contains(r)).ToList();
            if (candidates.Count == 0)
            {
                logger.LogDebug($"No scrap for '{lot.Name}': none of the scrap reasons is accepted at {lot.FlowPath}");
                return lot;
            }

            var reason = GetReason(candidates[random.Next(0, candidates.Count)]);
            mes.Materials.RecordWaferLosses(lot, scrapped, reason);

            var after = tracker.Reload(lot);
            if (after.UniversalState == UniversalState.Terminated || BatchQuantity(after) <= 0)
            {
                logger.LogInformation($"Scrap: all {count} wafer(s) of '{lot.Name}' scrapped ({reason.Name}), the lot leaves the line");
                return null;
            }

            logger.LogInformation($"Scrap: {count} wafer(s) of '{lot.Name}' scrapped ({reason.Name}), {BatchQuantity(after)} left");
            return after;
        }

        // "RWK_FUSE Photoresist TC:A:1/RwPGMA:1" -> flow "RWK_FUSE Photoresist TC"
        private static string FlowName(string? gotoFlowPath) => (gotoFlowPath ?? "").Split('/')[0].Split(':')[0];

        private static decimal BatchQuantity(Material lot) => Pipeline.BatchPlanner.LotQuantity(lot);

        private bool Roll(decimal probability) =>
            probability > 0 && random.Next(0, 1_000_000) < (int)(probability * 1_000_000);

        private HashSet<string> LoadStepLossReasons(long stepId)
        {
            var step = mes.MasterData.LoadRelations(mes.MasterData.GetById<Step>(stepId)!, "StepReason")!;
            return step.RelationCollection.ContainsKey("StepReason")
                ? step.RelationCollection["StepReason"].Cast<StepReason>()
                    .Select(r => mes.MasterData.GetById<Reason>(r.TargetEntity.Id)!)
                    .Where(r => r.ReasonType == ReasonType.Loss)
                    .Select(r => r.Name).ToHashSet(StringComparer.OrdinalIgnoreCase)
                : [];
        }

        private Reason GetReason(string name) =>
            _reasons.GetOrAdd(name, n => mes.MasterData.GetByName<Reason>(n)
                ?? throw new InvalidOperationException($"Reason '{n}' (Line:Chaos:ScrapReasons) not found"));
    }
}
