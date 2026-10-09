using Cmf.Foundation.BusinessObjects;
using Cmf.Navigo.BusinessObjects;
using Microsoft.Extensions.Logging;
using MESSimulator.Mes;

namespace MESSimulator.Steps
{
    /// <summary>
    /// Material movements on resources: dispatch/track-in, track-out and move-next, sub-material tracking, split and
    /// track-out, move next. Each one runs under the resource's lock and applies the resource's own state model
    /// (Standby to Productive on the first track-in, Productive to Standby when the last material leaves).
    /// </summary>
    public interface IMaterialTracker
    {
        Material Reload(Material material);
        MaterialCollection Reload(IEnumerable<Material> materials);
        Task<Material> DispatchAndTrackInAsync(Material lot, string resourceName);
        Task<Material> TrackOutAndMoveNextAsync(Material lot);

        /// <summary>The resource the lot is in process on (its last processed resource), or null.</summary>
        string? ProcessingResourceName(Material lot);

        /// <summary>Tracks the lot out without moving it on (it stays processed at the step).</summary>
        Task<Material> TrackOutAsync(Material lot);
        Task<MaterialCollection> TrackInSubMaterialsAsync(MaterialCollection subMaterials, string resourceName);
        Task TrackOutSubMaterialsAsync(MaterialCollection subMaterials, string resourceName);
        Task<Material> SplitAndTrackOutAsync(Material lot, IReadOnlyList<Material> subMaterials, bool isLastSplit);

        /// <summary>Splits <paramref name="quantity"/> off a lot without sub-materials into a new lot, tracked out (not moved).</summary>
        Task<Material> SplitQuantityAndTrackOutAsync(Material lot, decimal quantity, bool isLastSplit);

        /// <summary>
        /// Moves each lot to its next step; null when the first lot has no next step. Lots of one batch can come from
        /// different sub-flows (CureWafers runs in PHOTO FUSE and PHOTO POLYMIDE), so lots with the same next flow
        /// path are moved together.
        /// </summary>
        List<Material>? MoveNext(IReadOnlyList<Material> lots);

        /// <summary>Moves queued lots past their step without processing it (the MES skips the process, then moves them next).</summary>
        List<Material>? SkipAndMoveNext(IReadOnlyList<Material> lots);
    }

    public sealed class MaterialTracker(
        IMesGateway mes,
        StateModelCache stateModels,
        ResourceLocks locks,
        IRandomSource random,
        IOperatorCheckIn operatorCheckIn,
        SimulationClock clock,
        ILogger<MaterialTracker> logger) : IMaterialTracker
    {
        public Material Reload(Material material) =>
            mes.Materials.GetByName(material.Name) ?? throw new InvalidOperationException($"Material '{material.Name}' not found");

        public string? ProcessingResourceName(Material lot) =>
            lot.LastProcessedResource == null ? null : GetResourceById(lot.LastProcessedResource.Id).Name;

        public MaterialCollection Reload(IEnumerable<Material> materials) =>
            mes.Materials.GetByNames(materials.Select(m => m.Name));

        public async Task<Material> DispatchAndTrackInAsync(Material lot, string resourceName)
        {
            using var _ = await locks.AcquireAsync(resourceName);

            return WithFreshResource(() =>
            {
                var resource = GetResourceByName(resourceName);
                var stateModel = stateModels.ForResource(resource);
                var transition = resource.CurrentMainState.CurrentState.Name == "Productive"
                    ? null
                    : stateModel?.StateTransitions.Find(sm => sm.Name == "Standby to Productive");

                return mes.Materials.DispatchAndTrackIn(Reload(lot), resource, stateModel, transition, PickLane(resource));
            }, resourceName);
        }

        // The resource read before a call can change under it (another lot, or the MES itself): the MES then refuses the call
        // as "changed since last viewed", and repeating the same call with the same resource object (what MesCall does) can
        // not work. Read the resource again, and repeat.
        private const int staleResourceAttempts = 3;

        private T WithFreshResource<T>(Func<T> readAndCall, string resourceName) =>
            WithFreshResource(readAndCall, staleResourceAttempts, (ex, attempt) =>
                logger.LogDebug($"'{resourceName}' changed since it was read ({ex.Message}): reading it again ({attempt}/{staleResourceAttempts})"));

        /// <summary>Runs <paramref name="readAndCall"/> (which reads the resource and calls the MES) again when the MES says the data changed.</summary>
        public static T WithFreshResource<T>(Func<T> readAndCall, int attempts, Action<Exception, int>? onRetry = null)
        {
            for (int attempt = 1; ; attempt++)
            {
                try
                {
                    return readAndCall();
                }
                catch (Exception ex) when (attempt < attempts && MesCall.IsTransient(ex))
                {
                    onRetry?.Invoke(ex, attempt);
                }
            }
        }

        // A resource with multiple lanes active (e.g. the plating line) needs the lane to track in on: pick one at random
        private ResourceLane? PickLane(Resource resource)
        {
            if (resource.IsMultiLaneActive != true)
            {
                return null;
            }

            var lanes = mes.Resources.GetLanes(resource);
            if (lanes.Count == 0)
            {
                throw new InvalidOperationException($"'{resource.Name}' has multiple lanes active but no lanes");
            }

            var lane = lanes[random.Next(0, lanes.Count)];
            logger.LogDebug($"Using lane '{lane.Name}' of '{resource.Name}'");
            return lane;
        }

        public async Task<Material> TrackOutAndMoveNextAsync(Material lot)
        {
            var nextFlowPath = mes.Materials.GetTrackOutAndMoveNextFlowPath(lot)
                ?? throw new InvalidOperationException($"No next step to track out and move '{lot.Name}' to");

            var resourceName = GetResourceById(lot.LastProcessedResource.Id).Name;
            using var _ = await locks.AcquireAsync(resourceName);

            // Read inside the lock: the transition depends on what else is still in process
            return WithFreshResource(() =>
            {
                var resource = GetResourceById(lot.LastProcessedResource.Id);
                var stateModel = stateModels.ForResource(resource);
                var transition = resource.MaterialsInProcessCount != 0
                    ? null
                    : stateModel?.StateTransitions.Find(sm => sm.Name == "Productive to Standby");

                return mes.Materials.TrackOutAndMoveNext(Reload(lot), nextFlowPath, stateModel, transition);
            }, resourceName);
        }

        public async Task<Material> TrackOutAsync(Material lot)
        {
            var resourceName = GetResourceById(lot.LastProcessedResource.Id).Name;
            using var _ = await locks.AcquireAsync(resourceName);

            WithFreshResource(() =>
            {
                var resource = GetResourceById(lot.LastProcessedResource.Id);
                var stateModel = stateModels.ForResource(resource);
                // The lot leaving is all that is in process on the resource: back to Standby
                var transition = resource.MaterialsInProcessCount > 1
                    ? null
                    : stateModel?.StateTransitions.Find(sm => sm.Name == "Productive to Standby");

                return mes.Materials.TrackOut([Reload(lot)], stateModel, transition);
            }, resourceName);
            return Reload(lot);
        }

        public async Task<MaterialCollection> TrackInSubMaterialsAsync(MaterialCollection subMaterials, string resourceName)
        {
            using var _ = await locks.AcquireAsync(resourceName);

            var resource = GetResourceByName(resourceName);

            // A resource with process sub-resources (e.g. the WSM 150 wet strip chambers) needs the wafers tracked
            // in on one of them; otherwise (e.g. the peeler) on the resource itself
            var loaded = mes.MasterData.LoadRelations(resource, "SubResource")!;
            var processChambers = loaded.RelationCollection.ContainsKey("SubResource")
                ? loaded.RelationCollection["SubResource"].Cast<SubResource>().Select(s => s.TargetEntity).Where(r => r.ProcessingType == ProcessingType.Process).ToList()
                : [];
            if (processChambers.Count == 0)
            {
                return mes.Materials.TrackIn(Reload(subMaterials), stateModels.ForResource(resource));
            }

            var chamber = GetResourceById(processChambers[random.Next(0, processChambers.Count)].Id);
            logger.LogDebug($"Tracking in {subMaterials.Count} sub-material(s) on '{chamber.Name}' of '{resourceName}'");
            // A chamber can require its own operator check-in (the parent resource's doesn't cover it)
            return await operatorCheckIn.RunAsOperatorAsync(chamber.Name,
                () => Task.FromResult(mes.Materials.TrackIn(Reload(subMaterials), chamber, stateModels.ForResource(chamber), transition: null)),
                clock, logger);
        }

        public async Task TrackOutSubMaterialsAsync(MaterialCollection subMaterials, string resourceName)
        {
            using var _ = await locks.AcquireAsync(resourceName);

            var stateModel = stateModels.ForResource(GetResourceByName(resourceName));
            var parameters = Reload(subMaterials).ToDictionary(m => m, _ => new ComplexTrackOutParameters());
            mes.Materials.TrackOut(parameters, stateModel, transition: null);
        }

        /// <summary>
        /// Tracks out <paramref name="subMaterials"/> of the in-process <paramref name="lot"/> as a new lot
        /// (UI "Split and Track Out"). The parent stays in process with the remaining sub-materials, or terminates
        /// when none are left. On the <paramref name="isLastSplit"/> the resource goes back to Standby.
        /// </summary>
        public async Task<Material> SplitAndTrackOutAsync(Material lot, IReadOnlyList<Material> subMaterials, bool isLastSplit)
        {
            var resourceName = GetResourceById(lot.LastProcessStepResource.Id).Name;
            using var _ = await locks.AcquireAsync(resourceName);

            var splitSubMaterials = new SplitInputSubMaterialCollection();
            foreach (var subMaterial in subMaterials)
            {
                splitSubMaterials.Add(new SplitInputSubMaterial() { SubMaterial = new Material() { Id = subMaterial.Id } });
            }

            return WithFreshResource(() =>
            {
                var resource = GetResourceById(lot.LastProcessStepResource.Id);
                var stateModel = stateModels.ForResource(resource);
                var transition = isLastSplit && resource.MaterialsInProcessCount <= 1
                    ? stateModel?.StateTransitions.Find(sm => sm.Name == "Productive to Standby")
                    : null;

                return mes.Materials.TrackOut(new Dictionary<Material, ComplexTrackOutParameters>()
                {
                    {
                        Reload(lot),
                        new ComplexTrackOutParameters()
                        {
                            SplitAndTrackOutParameters = new SplitInputParameters()
                            {
                                PrimaryQuantity = 0,
                                SecondaryQuantity = 0,
                                SubMaterials = splitSubMaterials,
                            },
                            IsToSkipQuantityOverrideValidation = true,
                            // The parent terminates once its last sub-materials are split off
                            TerminateOnZeroQuantity = true
                        }
                    }
                }, stateModel, transition);
            }, resourceName);
        }

        public async Task<Material> SplitQuantityAndTrackOutAsync(Material lot, decimal quantity, bool isLastSplit)
        {
            var resourceName = GetResourceById(lot.LastProcessStepResource.Id).Name;
            using var _ = await locks.AcquireAsync(resourceName);

            return WithFreshResource(() =>
            {
                var resource = GetResourceById(lot.LastProcessStepResource.Id);
                var stateModel = stateModels.ForResource(resource);
                var transition = isLastSplit && resource.MaterialsInProcessCount <= 1
                    ? stateModel?.StateTransitions.Find(sm => sm.Name == "Productive to Standby")
                    : null;

                return mes.Materials.TrackOut(new Dictionary<Material, ComplexTrackOutParameters>()
                {
                    {
                        Reload(lot),
                        new ComplexTrackOutParameters()
                        {
                            SplitAndTrackOutParameters = new SplitInputParameters() { PrimaryQuantity = quantity },
                            // The parent terminates once its last quantity is split off
                            TerminateOnZeroQuantity = true
                        }
                    }
                }, stateModel, transition);
            }, resourceName);
        }

        public List<Material>? SkipAndMoveNext(IReadOnlyList<Material> lots)
        {
            var queued = new MaterialCollection();
            queued.AddRange(Reload(lots));
            mes.Materials.SkipProcess(queued);
            return MoveNext(lots);
        }

        public List<Material>? MoveNext(IReadOnlyList<Material> lots)
        {
            var reloaded = Reload(lots).ToList();
            var nextFlowPaths = reloaded.ToDictionary(m => m, m => mes.Materials.GetMoveNextFlowPath(m));
            if (nextFlowPaths[reloaded[0]] == null)
            {
                return null;
            }

            var moved = new List<Material>();
            foreach (var group in nextFlowPaths.Where(p => p.Value != null).GroupBy(p => p.Value!))
            {
                moved.AddRange(mes.Materials.MoveToNextStep(group.ToDictionary(p => p.Key, p => group.Key)));
                logger.LogInformation($"Moved {group.Count()} lot(s) to '{group.Key}'");
            }
            return moved;
        }

        private Resource GetResourceByName(string resourceName) =>
            mes.Resources.GetByName(resourceName) ?? throw new InvalidOperationException($"Resource '{resourceName}' not found");

        private Resource GetResourceById(long resourceId) =>
            mes.Resources.GetById(resourceId) ?? throw new InvalidOperationException($"Resource {resourceId} not found");
    }
}
