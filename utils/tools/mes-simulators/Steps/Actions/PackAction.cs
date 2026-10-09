using Cmf.Foundation.Common.Base;
using Cmf.Navigo.BusinessObjects;
using Microsoft.Extensions.Logging;
using MESSimulator.Mes;

namespace MESSimulator.Steps.Actions
{
    /// <summary>
    /// Packing steps (Wafer PACKING): the lot can only track out once all its quantity is packed. Right after the
    /// track-in, its wafers are packed by id (the MES creates the package, e.g. a Shielding Foil Packaging 360 holding
    /// up to 25 wafers, consuming the packaging consumable the feeders action attached). A lot already packed is skipped.
    /// </summary>
    public sealed class PackAction(IMesGateway mes, IMaterialTracker tracker, ILogger<PackAction> logger) : IStepAction
    {
        public const string ActionKey = "pack";
        public const StepHook ActionHook = StepHook.AfterTrackIn;

        public string Key => ActionKey;

        public StepHook Hook => ActionHook;

        public Task ExecuteAsync(StepContext context)
        {
            var lot = tracker.Reload(context.Lot);
            var info = mes.Materials.GetPackingInformation(lot);
            if (info.QuantityToBePacked is not > 0)
            {
                logger.LogDebug($"'{lot.Name}' is already packed ({info.PackedQuantity} {info.PackedQuantityUnits})");
                return Task.CompletedTask;
            }

            if (info.PackMaterialMode != PackMaterialMode.MaterialIds)
            {
                throw new InvalidOperationException($"Packing '{lot.Name}' in mode {info.PackMaterialMode} is not supported (only by material ids)");
            }

            // The MES still offers scrapped wafers (terminated, quantity 0): only the lot's live wafers are packed
            var live = mes.Materials.LoadChildren(lot).SubMaterials?.Cast<Material>()
                .Where(w => w.UniversalState != UniversalState.Terminated && w.PrimaryQuantity is not <= 0)
                .Select(w => w.Id).ToHashSet() ?? [];
            var wafers = new MaterialCollection();
            wafers.AddRange(info.PossibleTargetMaterials?.Cast<Material>().Where(w => live.Contains(w.Id)) ?? []);
            if (wafers.Count == 0)
            {
                throw new InvalidOperationException($"'{lot.Name}' has {info.QuantityToBePacked} {info.PackedQuantityUnits} to pack but none of its wafers can be packed (already in another package?)");
            }

            var package = mes.Materials.Pack(lot, wafers);
            logger.LogInformation($"Packed {wafers.Count} wafer(s) of '{lot.Name}' into '{package.Name}'");
            context.Lot = tracker.Reload(lot);
            return Task.CompletedTask;
        }
    }
}
