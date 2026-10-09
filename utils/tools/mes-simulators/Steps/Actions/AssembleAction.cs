using Cmf.Navigo.BusinessObjects;
using Microsoft.Extensions.Logging;
using MESSimulator.Mes;

namespace MESSimulator.Steps.Actions
{
    /// <summary>
    /// Mixed assembly steps (e.g. Base Asm, Install Od Coil Tubes): after the lot's track-in, consumes the lot's BOM
    /// source materials (assembled SFG sub-lots and consumables) into the lot with a single AssembleMaterial call,
    /// mirroring the MES "Perform Assembly" wizard. Reference material products that are not consumed carry an
    /// information value instead of a source material.
    /// </summary>
    public sealed class AssembleAction(IMesGateway mes, IMaterialTracker tracker, ILogger<AssembleAction> logger) : IStepAction
    {
        public const string ActionKey = "assemble";
        public const StepHook ActionHook = StepHook.AfterTrackIn;

        public string Key => ActionKey;

        public StepHook Hook => ActionHook;

        // Lots consume the same source materials (one kanban lot feeds many assemblies): one assembly at a time
        private readonly SemaphoreSlim _gate = new(1, 1);

        // A source material read before the call can still change under it (e.g. an automatic consumption elsewhere):
        // read everything again and repeat, a few times
        private const int staleAttempts = 3;

        public async Task ExecuteAsync(StepContext context)
        {
            await _gate.WaitAsync();
            try
            {
                for (int attempt = 1; ; attempt++)
                {
                    try
                    {
                        Assemble(context);
                        return;
                    }
                    catch (Exception ex) when (attempt < staleAttempts && IsStale(ex))
                    {
                        logger.LogDebug($"Assembling '{context.Lot.Name}' at {context.Step.Name} hit stale data ({ex.Message}): attempt {attempt + 1}");
                    }
                }
            }
            finally
            {
                _gate.Release();
            }
        }

        private bool IsRawMaterial(Material material) =>
            material.Product != null && mes.MasterData.GetById<Product>(material.Product.Id)?.ProductType == ProductType.RawMaterial;

        private static bool IsStale(Exception ex) =>
            ex.Message.Contains("changed by another user", StringComparison.OrdinalIgnoreCase)
            || ex.Message.Contains("has changed since last viewed", StringComparison.OrdinalIgnoreCase);

        private void Assemble(StepContext context)
        {
            // LevelsToLoad 2 loads the lot's current BOM instance and its BOM
            var lot = mes.Materials.LoadBasicInformation(tracker.Reload(context.Lot));
            var bomInstance = lot.CurrentBOMInstance
                ?? throw new InvalidOperationException($"'{lot.Name}' has no BOM instance to assemble at {context.Step.Name}");

            var sourceMaterials = mes.Materials.GetBomMaterialsForAssemble(lot);
            var bomInstanceItems = mes.Materials.GetBomInstanceItems(bomInstance);

            var assembleMaterials = new AssembleMaterialCollection();
            var bomProductsInAssemble = new BOMProductInAssembleCollection();

            foreach (var item in bomInstanceItems)
            {
                // A reference product is information only (no source material is consumed)
                if (item.IsReference == true)
                {
                    bomProductsInAssemble.Add(new BOMProductInAssemble()
                    {
                        BOMInstanceItem = item,
                        BOMProduct = item.BOMProduct,
                        InformationValue = Guid.NewGuid().ToString()
                    });
                    continue;
                }

                var candidates = sourceMaterials.Where(s => s.BOMProductId == item.BOMProduct.Id)
                    .Select(s => mes.Materials.GetByName(s.MaterialName)
                        ?? throw new InvalidOperationException($"Source material '{s.MaterialName}' for '{lot.Name}' not found"))
                    .ToList();
                if (candidates.Count == 0)
                {
                    throw new InvalidOperationException($"No source material for BOM product '{item.BOMProduct.Name}' to assemble '{lot.Name}'");
                }

                var required = (decimal)(item.RequiredQuantity ?? 0);

                logger.LogDebug($"'{lot.Name}' needs {required:0.##} '{item.BOMProduct.Name}' at {context.Step.Name}: {string.Join(", ", candidates.Select(c => $"{c.Name} ({c.PrimaryQuantity:0.##})"))}");

                // A raw material (e.g. NITROGEN-3000PSI) running low is replenished, as the set-up wizard would;
                // semi-finished goods are real production output and are never topped up
                if (candidates.Sum(c => c.PrimaryQuantity ?? 0) < required && IsRawMaterial(candidates[0]))
                {
                    candidates[0] = mes.Materials.ChangeQuantity(candidates[0], (candidates[0].PrimaryQuantity ?? 0) + required * 100);
                    logger.LogInformation($"Replenished raw material '{candidates[0].Name}' to {candidates[0].PrimaryQuantity:0.##} to assemble '{lot.Name}'");
                }

                // The required quantity can span several source lots (each kanban lot holds a few units)
                decimal remaining = required;
                foreach (var source in candidates.Where(c => c.PrimaryQuantity > 0))
                {
                    if (remaining <= 0)
                    {
                        break;
                    }

                    decimal take = Math.Min(remaining, source.PrimaryQuantity!.Value);
                    assembleMaterials.Add(new AssembleMaterial()
                    {
                        BOMInstanceItem = item,
                        BOMProduct = item.BOMProduct,
                        SourceProduct = item.Product,
                        Material = source,
                        Quantity = take
                    });
                    remaining -= take;
                }

                if (remaining > 0)
                {
                    throw new InvalidOperationException($"Not enough '{item.BOMProduct.Name}' to assemble '{lot.Name}': {required - remaining:0.##} of {required:0.##} available");
                }
            }

            context.Lot = mes.Materials.Assemble(lot, assembleMaterials, bomProductsInAssemble);
            logger.LogInformation($"Assembled '{context.Lot.Name}' from {assembleMaterials.Count} BOM material(s)");
        }
    }
}
