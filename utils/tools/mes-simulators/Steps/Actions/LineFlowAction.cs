using Cmf.Foundation.BusinessObjects;
using Cmf.Navigo.BusinessObjects;
using Microsoft.Extensions.Logging;
using MESSimulator.Line;
using MESSimulator.Mes;

namespace MESSimulator.Steps.Actions
{
    /// <summary>
    /// Line steps (e.g. METAL PLATING on the Semitool Raider): the lot's track-in assembles its wafers into the line
    /// flow (e.g. Clean-Cu-SRD), and the lot can only track out once every wafer went through it. Right after the
    /// track-in, the wafers run each line flow step together on one of the chambers the MES allows for the lot (those
    /// of its lane). It starts from wherever the wafers are, so a lot left in process on the line can be resumed.
    /// </summary>
    public sealed class LineFlowAction(
        IMesGateway mes,
        IMaterialTracker tracker,
        StateModelCache stateModels,
        ResourceLocks locks,
        SimulationClock clock,
        IRandomSource random,
        IOperatorCheckIn operatorCheckIn,
        ILogger<LineFlowAction> logger) : IStepAction
    {
        public const string ActionKey = "lineFlow";
        public const StepHook ActionHook = StepHook.AfterTrackIn;

        public string Key => ActionKey;

        public StepHook Hook => ActionHook;

        // It starts from wherever the wafers are
        public bool Resumable => true;

        public async Task ExecuteAsync(StepContext context)
        {
            var lot = mes.Materials.LoadChildren(tracker.Reload(context.Lot));
            var lineFlow = (lot.LineFlowVersion == null ? null : mes.MasterData.GetById<Flow>(lot.LineFlowVersion.Id))
                ?? throw new InvalidOperationException($"'{lot.Name}' has no line flow at {context.Step.Name}");
            var steps = mes.Setup.GetFlowSteps(lineFlow);

            // Chambers allowed for each line step (the lot's lane)
            var allowedChambers = mes.Setup.GetLineStepResources(lot);

            // The wafers still in the lot (scrapped or terminated ones don't go through the line)
            var wafers = Active(tracker.Reload(lot.SubMaterials.Cast<Material>()));
            if (wafers.Count == 0)
            {
                throw new InvalidOperationException($"'{lot.Name}' has no wafers to run through line flow {lineFlow.Name}");
            }
            for (int i = StepIndex(steps, wafers[0]); i < steps.Count; i++)
            {
                Resource chamber;
                if (wafers[0].SystemState == MaterialSystemState.InProcess)
                {
                    // Resumed: the wafers are already on a chamber of this step
                    chamber = mes.Resources.GetById(wafers[0].LastProcessedResource.Id)
                        ?? throw new InvalidOperationException($"The chamber '{wafers[0].Name}' is in process on was not found");
                }
                else
                {
                    var candidates = allowedChambers.GetValueOrDefault(steps[i].Name) ?? [];
                    if (candidates.Count == 0)
                    {
                        throw new InvalidOperationException($"No chamber of '{context.ResourceName}' allowed for '{lot.Name}' at line step '{steps[i].Name}'");
                    }
                    chamber = mes.Resources.GetById(candidates[random.Next(0, candidates.Count)].Id)!;

                    logger.LogDebug($"Line {lineFlow.Name}/{steps[i].Name}: {wafers.Count} wafer(s) of '{lot.Name}' on '{chamber.Name}'");
                    // A chamber can require its own operator check-in
                    var waferIn = wafers;
                    var chamberIn = chamber;
                    wafers = Active(await operatorCheckIn.RunAsOperatorAsync(chamber.Name, () => TrackInAsync(waferIn, chamberIn), clock, logger));
                    await Task.Delay(clock.RandomDuration(random, context.Step.LineStepMinSeconds, context.Step.LineStepMaxSeconds));
                }

                var waferOut = wafers;
                var chamberOut = chamber;
                wafers = Active(await operatorCheckIn.RunAsOperatorAsync(chamber.Name, () => TrackOutAsync(waferOut, chamberOut), clock, logger));
                logger.LogDebug($"Line {lineFlow.Name}/{steps[i].Name}: done, wafers now at {wafers[0].FlowPath} ({wafers[0].SystemState})");
            }

            logger.LogInformation($"'{lot.Name}' went through line flow {lineFlow.Name} ({string.Join(" > ", steps.Select(s => s.Name))})");
            context.Lot = tracker.Reload(lot);
        }

        private static MaterialCollection Active(MaterialCollection wafers)
        {
            var active = new MaterialCollection();
            active.AddRange(wafers.Where(w => w.UniversalState != Cmf.Foundation.Common.Base.UniversalState.Terminated));
            return active;
        }

        // The line step the wafers are at; a processed wafer at the last step is done
        private static int StepIndex(List<Step> steps, Material wafer)
        {
            var stepName = LineDefinition.CurrentStepName(wafer);
            int index = steps.FindIndex(s => string.Equals(s.Name, stepName, StringComparison.OrdinalIgnoreCase));
            if (index < 0)
            {
                throw new InvalidOperationException($"Wafer '{wafer.Name}' is at '{wafer.FlowPath}', not in the line flow");
            }
            return wafer.SystemState == MaterialSystemState.Processed && index == steps.Count - 1 ? steps.Count : index;
        }

        private async Task<MaterialCollection> TrackInAsync(MaterialCollection wafers, Resource chamber)
        {
            using var _ = await locks.AcquireAsync(chamber.Name);

            chamber = mes.Resources.GetById(chamber.Id)!;
            var (stateModel, transition) = Transition(chamber, from: "Standby", to: "Productive");

            // The wafers are already dispatched to the line: track them in on the chamber
            return mes.Materials.TrackIn(tracker.Reload(wafers), chamber, stateModel, transition);
        }

        // In a line flow the MES moves the wafers on to the next line step at track-out
        private async Task<MaterialCollection> TrackOutAsync(MaterialCollection wafers, Resource chamber)
        {
            using var _ = await locks.AcquireAsync(chamber.Name);

            wafers = tracker.Reload(wafers);
            chamber = mes.Resources.GetById(chamber.Id)!;
            // The wafers leaving are all that is in process on the chamber: back to Standby
            var (stateModel, transition) = chamber.MaterialsInProcessCount > wafers.Count
                ? (null, null)
                : Transition(chamber, from: "Productive", to: "Standby");

            mes.Materials.TrackOut(wafers, stateModel, transition);
            return tracker.Reload(wafers);
        }

        // Chambers are shared by the lots of a lane, so their state may already have moved on: only ask for a
        // transition from the state the chamber is actually in (none otherwise)
        private (StateModel? StateModel, StateModelTransition? Transition) Transition(Resource chamber, string from, string to)
        {
            if (chamber.CurrentMainState?.CurrentState?.Name != from)
            {
                return (null, null);
            }

            var stateModel = stateModels.ForResource(chamber);
            var transition = stateModel?.StateTransitions.Find(t => t.Name == $"{from} to {to}");
            return transition == null ? (null, null) : (stateModel, transition);
        }
    }
}
