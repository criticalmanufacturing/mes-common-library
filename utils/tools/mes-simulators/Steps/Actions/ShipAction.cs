using Cmf.Navigo.BusinessObjects;
using Microsoft.Extensions.Logging;
using MESSimulator.Mes;

namespace MESSimulator.Steps.Actions
{
    /// <summary>
    /// Ships a lot to the next step when that step is in another facility.
    /// </summary>
    public interface IShipper
    {
        /// <summary>
        /// Ships the lot to its facility's shipping facility (<paramref name="destination"/> when there are several) and
        /// receives it there at its next step.
        /// </summary>
        Material Ship(Material lot, string? destination = null, bool receive = true);
    }

    /// <summary>
    /// Ship &amp; track-out (SORTER): the next step (Wafer PACKING) belongs to another facility, which the MES only
    /// reaches with the Ship operation. Instead of a track-out and move-next, the lot is tracked out, shipped to its
    /// facility's shipping facility (Production FE SC ships to Warehouse FE SC) and received there at the next step.
    /// </summary>
    public sealed class ShipAction(IMesGateway mes, IMaterialTracker tracker, ILogger<ShipAction> logger) : IStepAction, IShipper
    {
        public const string ActionKey = "ship";
        public const StepHook ActionHook = StepHook.TrackOut;

        public string Key => ActionKey;

        public StepHook Hook => ActionHook;

        public async Task ExecuteAsync(StepContext context)
        {
            var lot = await tracker.TrackOutAsync(context.Lot);
            logger.LogInformation($"Tracked Out {context.Step.Name} {lot.Name}");
            context.Lot = Ship(lot, context.Step.ShipTo);
        }

        /// <summary>
        /// Ships a lot (processed, or queued at a pass-through step) to its facility's (only) shipping facility and
        /// receives it there at its next step. A lot already in transit is only received.
        /// </summary>
        public Material Ship(Material lot, string? destination = null, bool receive = true)
        {
            lot = tracker.Reload(lot);
            if (lot.SystemState == MaterialSystemState.InTransit)
            {
                if (!receive)
                {
                    return lot;
                }
                // Shipped earlier but not received: the MES gives no move-next path for a lot in transit
                return Receive(lot, NextStepInParentFlow(lot));
            }

            // Only a lot that is received needs a next step: at the end of the line (SHIPPING FINAL CUSTOMER, the last step
            // of its flow) the lot is shipped to a facility that has no flow
            var nextFlowPath = receive
                ? mes.Materials.GetMoveNextFlowPath(lot) ?? throw new InvalidOperationException($"No next step to ship '{lot.Name}' to")
                : null;

            var facility = mes.MasterData.GetById<Facility>(lot.Facility.Id)!;
            var loaded = mes.MasterData.LoadRelations(facility, "ShippingFacility")!;
            var destinations = loaded.RelationCollection.ContainsKey("ShippingFacility")
                ? loaded.RelationCollection["ShippingFacility"].Cast<ShippingFacility>().Select(s => mes.MasterData.GetById<Facility>(s.TargetEntity.Id)!).ToList()
                : [];
            var target = destination != null
                ? destinations.FirstOrDefault(f => string.Equals(f.Name, destination, StringComparison.OrdinalIgnoreCase))
                    ?? throw new InvalidOperationException($"'{facility.Name}' cannot ship to '{destination}' (it ships to {string.Join(", ", destinations.Select(f => $"'{f.Name}'"))})")
                : destinations.Count == 1
                    ? destinations[0]
                    : throw new InvalidOperationException($"'{facility.Name}' ships to {string.Join(", ", destinations.Select(f => $"'{f.Name}'"))}: set ShipTo on the step in simulationConfiguration.json");

            mes.Materials.Ship([lot], target);
            logger.LogInformation($"Shipped '{lot.Name}' from '{facility.Name}' to '{target.Name}'");
            return receive ? Receive(tracker.Reload(lot), nextFlowPath!) : tracker.Reload(lot);
        }

        // "Y31_CSP_3L:A:1/SORTER:6" -> "Y31_CSP_3L:A:1/Wafer PACKING:7": the step after the lot's one in the same flow
        private string NextStepInParentFlow(Material lot)
        {
            int lastSlash = lot.FlowPath.LastIndexOf('/');
            var parentPath = lot.FlowPath[..lastSlash];
            var (stepName, position) = (lot.FlowPath[(lastSlash + 1)..].Split(':')[0], int.Parse(lot.FlowPath.Split(':')[^1]));

            var flow = mes.MasterData.GetByName<Flow>(parentPath[(parentPath.LastIndexOf('/') + 1)..].Split(':')[0])!;
            var steps = mes.Setup.GetFlowSteps(flow);
            int index = steps.FindIndex(s => string.Equals(s.Name, stepName, StringComparison.OrdinalIgnoreCase));
            if (index < 0 || index + 1 >= steps.Count)
            {
                throw new InvalidOperationException($"No step after '{stepName}' in '{flow.Name}' to receive '{lot.Name}' at");
            }
            return $"{parentPath}/{steps[index + 1].Name}:{position + 1}";
        }

        private Material Receive(Material lot, string flowPath)
        {
            mes.Materials.Receive(lot, flowPath);
            lot = tracker.Reload(lot);
            logger.LogInformation($"Received '{lot.Name}' at {lot.FlowPath}");
            return lot;
        }
    }
}
