using Cmf.Navigo.BusinessObjects;
using Cmf.Navigo.BusinessOrchestration.MaterialLogisticsManagement.InputObjects;
using Microsoft.Extensions.Logging;
using MESSimulator.Mes;

namespace MESSimulator.Steps.Actions
{
    /// <summary>
    /// Whole-lot packing steps (e.g. RTU PACKING): after the track-in, packs the lot's quantity — one package per unit — by
    /// quantity (no sub-materials), unlike the wafer-id <c>pack</c> action. The lot can only track out once everything is packed.
    /// Resumable: it packs only what the MES says is still to be packed.
    /// </summary>
    public sealed class LotPackAction(IMesGateway mes, IMaterialTracker tracker, ILogger<LotPackAction> logger) : IStepAction
    {
        public const string ActionKey = "lotPack";
        public const StepHook ActionHook = StepHook.AfterTrackIn;

        public string Key => ActionKey;

        public StepHook Hook => ActionHook;

        public bool Resumable => true;

        public Task ExecuteAsync(StepContext context)
        {
            var lot = tracker.Reload(context.Lot);
            // What is left to pack (a run that stopped half-way already packed the rest); the lot's quantity when the MES
            // doesn't say
            int quantity = decimal.ToInt32(mes.Materials.GetPackingInformation(lot)?.QuantityToBePacked ?? lot.PrimaryQuantity ?? 0);
            if (quantity <= 0)
            {
                logger.LogDebug($"'{lot.Name}' is already packed");
            }

            for (int i = 0; i < quantity; i++)
            {
                lot = mes.Materials.PackLot(lot);
                logger.LogInformation($"Packed unit {i + 1}/{quantity} of '{lot.Name}'");
            }

            context.Lot = lot;
            return Task.CompletedTask;
        }
    }

    /// <summary>
    /// Palletizing steps (e.g. RTU FINAL PACKAGING): after the track-in, nests the material's packages (the boxes packed by
    /// <see cref="LotPackAction"/>) into parent packages of the MES's next packing level, up to that level's maximum quantity,
    /// then closes them. Mirrors the MES multi-level packing wizard. Resumable: packages already nested (and the pallets
    /// themselves) are left as they are.
    /// </summary>
    public sealed class PalletPackAction(IMesGateway mes, IMaterialTracker tracker, ILogger<PalletPackAction> logger) : IStepAction
    {
        public const string ActionKey = "palletPack";
        public const StepHook ActionHook = StepHook.AfterTrackIn;

        public string Key => ActionKey;

        public StepHook Hook => ActionHook;

        public bool Resumable => true;

        public Task ExecuteAsync(StepContext context)
        {
            var lot = tracker.Reload(context.Lot);

            var level = mes.Materials.GetMultiLevelPackingInformation(lot).FirstOrDefault()
                ?? throw new InvalidOperationException($"No multi-level packing level for '{lot.Name}' at {context.Step.Name}");
            var packageProduct = level.PackageProduct;
            int maxQuantity = (int)(level.MaximumQuantity ?? 0);
            if (maxQuantity <= 0)
            {
                throw new InvalidOperationException(
                    $"The packing level of '{lot.Name}' at {context.Step.Name} ({packageProduct?.Name}) has no maximum quantity: set it in the MES to palletize");
            }

            // The boxes not on a pallet yet (a run that stopped half-way nested some already); never the pallets themselves
            var materialPackages = mes.Materials.GetMaterialPackages(lot.Id)
                .Where(p => p.ParentPackage == null && !string.Equals(p.Product?.Name, packageProduct?.Name, StringComparison.OrdinalIgnoreCase))
                .ToList();
            if (materialPackages.Count == 0)
            {
                logger.LogDebug($"'{lot.Name}' has no packages left to palletize");
                context.Lot = lot;
                return Task.CompletedTask;
            }
            var chunks = materialPackages.Chunk(maxQuantity).ToList();

            var parameters = new CreatePackageParametersCollection();
            foreach (var chunk in chunks)
            {
                parameters.Add(new CreatePackageParameters()
                {
                    Mode = PackingContentType.Packages,
                    Product = packageProduct
                });
            }

            var parentPackages = mes.Materials.CreatePackages(parameters);

            for (int i = 0; i < chunks.Count; i++)
            {
                var childPackages = new PackageCollection();
                childPackages.AddRange(chunks[i]);
                mes.Materials.AddPackagesToPackage(parentPackages[i], childPackages, lot);
            }

            mes.Materials.ClosePackages(lot, parentPackages);
            logger.LogInformation($"Palletized '{lot.Name}': {parentPackages.Count} parent package(s) across {chunks.Count} pallet(s)");

            context.Lot = tracker.Reload(lot);
            return Task.CompletedTask;
        }
    }
}
