using System.Collections.Concurrent;
using Cmf.Foundation.BusinessObjects.QueryObject;
using Cmf.Foundation.Common;
using Cmf.Navigo.BusinessObjects;
using Microsoft.Extensions.Logging;
using MESSimulator.Line;
using MESSimulator.Mes;

namespace MESSimulator.Steps.Actions
{
    /// <summary>
    /// Automatic consumption steps: before the track-in, makes sure the resource's feeders offer every consumable the
    /// lot's BOM needs at this step (attaching a dispatchable one to an empty feeder, like the Perform Setup wizard,
    /// or a newly created one when none can be dispatched), and replaces attached consumables that run low.
    /// Quantities and the loss reason come from Line:Consumables in simulationConfiguration.json, per product.
    /// On a line resource (e.g. the METAL PLATING Semitool Raider) the consumption happens in the chambers that run the
    /// lot's line flow, through feeders shared by the chambers of both lanes: each of those feeders gets a consumable
    /// from its own dispatch list (the setup wizard resolves no BOM for line steps).
    /// </summary>
    public sealed class FeedersAction(IMesGateway mes, IMaterialTracker tracker, LineDefinition line, IRandomSource random, SetUpLots setUpLots,
        SharedFeeders sharedFeeders, ILogger<FeedersAction> logger) : IStepAction
    {
        private readonly ConcurrentDictionary<string, Reason> _reasons = new();

        // Feeders are shared between resources (e.g. Coater Feeder-001 under both SUSS coaters): one preparation at a time
        private readonly SemaphoreSlim _gate = new(1, 1);

        public const string ActionKey = "feeders";
        public const StepHook ActionHook = StepHook.BeforeTrackIn;

        public string Key => ActionKey;

        public StepHook Hook => ActionHook;

        // The resource can change while the feeders are prepared (another lot tracks in or out on it): start over
        private const int staleRetries = 3;

        public async Task ExecuteAsync(StepContext context)
        {
            await _gate.WaitAsync();
            try
            {
                for (int attempt = 1; ; attempt++)
                {
                    try
                    {
                        PrepareFeeders(context.Lot, context.ResourceName, context.Step.FeederProducts);
                        // Set up but not tracked in yet: no other lot may swap this resource's shared feeders meanwhile (the
                        // executor releases the lot after its track-in). Registered inside the gate, before the next lot's turn.
                        setUpLots.Add(context.ResourceName, context.Lot.Name);
                        return;
                    }
                    catch (Exception ex) when (attempt < staleRetries && IsStale(ex))
                    {
                        logger.LogDebug($"Feeders of '{context.ResourceName}' changed while preparing them for '{context.Lot.Name}', starting over ({attempt}/{staleRetries}): {ex.Message}");
                    }
                }
            }
            finally
            {
                _gate.Release();
            }
        }

        private static bool IsStale(Exception ex) =>
            ex.Message.Contains("has been changed by another user", StringComparison.OrdinalIgnoreCase)
            || ex.Message.Contains("has changed since last viewed", StringComparison.OrdinalIgnoreCase);

        private void PrepareFeeders(Material material, string resourceName, IReadOnlyList<string>? feederProducts = null)
        {
            var resource = mes.MasterData.LoadRelations(GetResourceByName(resourceName), "SubResource")!;
            if (resource.ProcessingType == ProcessingType.Line)
            {
                PrepareLineFeeders(material, resource);
                return;
            }

            var feeders = resource.RelationCollection["SubResource"].Cast<SubResource>().Select(x => x.TargetEntity).Where(x => x.Type == "Feeder").ToList();

            // Consumable currently attached to each feeder (null when the feeder is empty), and every attached one
            var feederConsumables = new Dictionary<Resource, Material?>();
            var feederAttached = new Dictionary<Resource, List<Material>>();
            for (int i = 0; i < feeders.Count; i++)
            {
                feeders[i] = mes.MasterData.LoadRelations(feeders[i], "MaterialResource")!;

                var attached = feeders[i].RelationCollection.ContainsKey("MaterialResource")
                    ? feeders[i].RelationCollection["MaterialResource"].Cast<MaterialResource>()
                        .Select(mr => tracker.Reload(mr.SourceEntity)).ToList()
                    : [];
                feederAttached[feeders[i]] = attached;
                feederConsumables[feeders[i]] = attached.FirstOrDefault();
            }

            // Attach a consumable only for BOM products the material needs here that no feeder already offers
            var (bomProducts, bomProductToConsumableFeed) = GetConsumableBomProducts(resource, material);

            // A mixed-assembly step (e.g. FINAL WIRING) consumes only some BOM products from feeders (Screws, automatically
            // at track-in); the others are assembled by hand: Line:Steps:{step}:FeederProducts lists the fed ones
            if (feederProducts is { Count: > 0 })
            {
                bomProducts = bomProducts.Where(b => feederProducts.Contains(GetProductName(b.TargetEntity) ?? "", StringComparer.OrdinalIgnoreCase)).ToList();
            }

            var neededProducts = bomProducts.Select(b => GetProductName(b.TargetEntity)).ToHashSet(StringComparer.OrdinalIgnoreCase);
            foreach (var bomProduct in bomProducts)
            {
                var productName = GetProductName(bomProduct.TargetEntity);

                if (bomProductToConsumableFeed.ContainsKey(bomProduct.Id)
                    || feederConsumables.Values.Any(c => c != null && GetProductName(c.Product) == productName))
                {
                    logger.LogDebug($"'{productName}' already offered by a consumable feed on '{resourceName}'");
                    continue;
                }

                var emptyFeeders = feederConsumables.Where(f => f.Value == null).Select(f => f.Key).ToList();
                if (emptyFeeders.Count == 0)
                {
                    // Shared feeders (e.g. a developer's single feeder used by PI Spray and AZ Spray): free a feeder
                    // whose consumable this material does not need. It is only detached (it is still good), and only
                    // when nothing is in process or set up on any resource sharing it, so no lot loses its consumable.
                    var swappable = feederAttached
                        .Where(f => f.Value.Count > 0 && f.Value.All(c => !neededProducts.Contains(GetProductName(c.Product) ?? "")))
                        .Select(f => f.Key).ToList();
                    if (swappable.Count == 0)
                    {
                        throw new ResourceUnsuitableException($"no empty feeder for '{productName}', and every feeder holds a consumable the material needs");
                    }
                    var busy = swappable.Select(f => sharedFeeders.WhyBusy(f, resourceName, material.Name)).ToList();
                    emptyFeeders = swappable.Where((_, i) => busy[i] == null).ToList();
                    if (emptyFeeders.Count == 0)
                    {
                        throw new ResourceUnsuitableException($"its feeder(s) cannot be swapped to '{productName}': {busy.First()}", temporary: true);
                    }
                }

                // Like the UI, ask each feeder for its dispatch list: it only returns consumables whose
                // RequiredService the feeder provides (e.g. 'Feed PI Spray'), so the attach can resolve it
                Resource? emptyFeeder = null;
                Material? consumable = null;
                foreach (var feeder in emptyFeeders)
                {
                    var candidates = GetDispatchableConsumables(feeder, bomProduct);
                    if (candidates.Count > 0)
                    {
                        emptyFeeder = feeder;
                        consumable = candidates[random.Next(0, candidates.Count)];
                        break;
                    }
                }

                if (emptyFeeder == null || consumable == null)
                {
                    // Nothing to dispatch (e.g. the last one is on another resource): create one like an existing material of the product
                    var created = CreateConsumableFor(bomProduct, productName, resourceName, emptyFeeders);
                    consumable = created;
                    emptyFeeder = emptyFeeders.FirstOrDefault(feeder => GetDispatchableConsumables(feeder, bomProduct).Any(c => c.Name == created.Name))
                        ?? throw new InvalidOperationException($"New '{productName}' material '{created.Name}' is not dispatchable to the empty feeder(s) {string.Join(", ", emptyFeeders.Select(f => $"'{f.Name}'"))} on '{resourceName}'");
                }

                var toDetach = feederAttached[emptyFeeder];
                if (toDetach.Count > 0)
                {
                    logger.LogDebug($"Swapping {string.Join(", ", toDetach.Select(c => $"'{c.Name}'"))} on shared feeder '{emptyFeeder.Name}' for '{consumable.Name}' ({productName})");
                }
                else
                {
                    logger.LogDebug($"Attaching '{consumable.Name}' ({productName}) to feeder '{emptyFeeder.Name}'");
                }

                // Same call as the UI setup wizard: attach (and detach) on the main resource, in the context of the material
                var detach = new MaterialCollection();
                detach.AddRange(toDetach.Select(tracker.Reload));
                mes.Resources.ManageConsumableFeeds(resource, tracker.Reload(material),
                    consumablesToAttach: new Dictionary<Resource, MaterialCollection>() { { emptyFeeder, [consumable] } },
                    consumablesToDetach: toDetach.Count > 0 ? new Dictionary<Resource, MaterialCollection>() { { emptyFeeder, detach } } : null);
                logger.LogInformation(toDetach.Count > 0
                    ? $"Swapped {string.Join(", ", toDetach.Select(c => $"'{c.Name}'"))} for '{consumable.Name}' ({productName}) on feeder '{emptyFeeder.Name}'"
                    : $"Attached '{consumable.Name}' ({productName}) to feeder '{emptyFeeder.Name}'");

                var attachedConsumable = tracker.Reload(consumable);
                feederConsumables[emptyFeeder] = attachedConsumable;
                feederAttached[emptyFeeder] = [attachedConsumable];
            }

            // A consumable running low is replaced by a new one
            foreach (var (feeder, consumables) in feederAttached)
            {
                foreach (var consumable in consumables)
                {
                    var rules = line.Consumables.For(GetProductName(consumable.Product));
                    if (consumable.PrimaryQuantity < rules.LowQuantity)
                    {
                        // Replacing terminates the old consumable: not while a lot on a resource sharing the feeder uses it
                        // (it still has some left; the next lot replaces it)
                        if (sharedFeeders.WhyBusy(feeder, resourceName, material.Name) is { } busy)
                        {
                            logger.LogDebug($"'{consumable.Name}' on feeder '{feeder.Name}' runs low, but is not replaced now: {busy}");
                            continue;
                        }
                        ReplaceConsumable(resource, material, feeder, consumable, rules);
                    }
                }
            }
        }

        /// <summary>
        /// Line resource: every feeder of the chambers that run the material's line flow steps holds a consumable
        /// (attached from the feeder's dispatch list when empty, replaced when low). A feeder shared by several
        /// chambers is handled once.
        /// </summary>
        private void PrepareLineFeeders(Material material, Resource lineResource)
        {
            var lineFlow = mes.Setup.ResolveLineFlow(tracker.Reload(material))
                ?? throw new InvalidOperationException($"No line flow resolved for '{material.Name}' on '{lineResource.Name}'");

            var chambers = SubResources(lineResource).ToDictionary(r => r.Id);
            var handled = new HashSet<long>();
            foreach (var step in mes.Setup.GetFlowSteps(lineFlow))
            {
                foreach (var chamber in mes.Setup.GetResourcesForStep(step).Where(r => chambers.ContainsKey(r.Id)).Select(r => chambers[r.Id]))
                {
                    foreach (var feeder in SubResources(chamber).Where(r => r.Type == "Feeder" && handled.Add(r.Id)))
                    {
                        PrepareLineFeeder(material, chamber, feeder, $"{lineFlow.Name}/{step.Name}");
                    }
                }
            }
        }

        private void PrepareLineFeeder(Material material, Resource chamber, Resource feeder, string where)
        {
            var loaded = mes.MasterData.LoadRelations(feeder, "MaterialResource")!;
            var attached = loaded.RelationCollection.ContainsKey("MaterialResource")
                ? loaded.RelationCollection["MaterialResource"].Cast<MaterialResource>().Select(mr => tracker.Reload(mr.SourceEntity)).ToList()
                : [];

            if (attached.Count == 0)
            {
                var candidates = mes.Setup.GetDispatchList(feeder, new FilterCollection()
                {
                    new Filter() { Name = "IsApproved", Operator = FieldOperator.IsEqualTo, Value = true, LogicalOperator = LogicalOperator.AND }
                });
                if (candidates.Count == 0)
                {
                    throw new InvalidOperationException($"No dispatchable consumable for feeder '{feeder.Name}' of '{chamber.Name}' ({where})");
                }

                var consumable = candidates[random.Next(0, candidates.Count)];
                var productName = GetProductName(consumable.Product);
                logger.LogDebug($"Attaching '{consumable.Name}' ({productName}) to shared feeder '{feeder.Name}' via '{chamber.Name}' ({where})");
                mes.Resources.ManageConsumableFeeds(chamber, tracker.Reload(material),
                    consumablesToAttach: new Dictionary<Resource, MaterialCollection>() { { feeder, [consumable] } });
                logger.LogInformation($"Attached '{consumable.Name}' ({productName}) to feeder '{feeder.Name}' ({where})");
                attached = [tracker.Reload(consumable)];
            }

            foreach (var consumable in attached)
            {
                var rules = line.Consumables.For(GetProductName(consumable.Product));
                if (consumable.PrimaryQuantity < rules.LowQuantity)
                {
                    // The line's feeders are shared by the chambers of both lanes: not while wafers use it
                    if (sharedFeeders.WhyBusy(feeder, chamber.Name, material.Name) is { } busy)
                    {
                        logger.LogDebug($"'{consumable.Name}' on feeder '{feeder.Name}' runs low, but is not replaced now: {busy}");
                        continue;
                    }
                    ReplaceConsumable(chamber, material, feeder, consumable, rules);
                }
            }
        }

        private List<Resource> SubResources(Resource resource)
        {
            var loaded = mes.MasterData.LoadRelations(resource, "SubResource")!;
            return loaded.RelationCollection.ContainsKey("SubResource")
                ? loaded.RelationCollection["SubResource"].Cast<SubResource>().Select(s => s.TargetEntity).ToList()
                : [];
        }

        /// <summary>
        /// A new, dispatchable consumable of the BOM product, created like a material of that product one of the
        /// feeders can dispatch (e.g. in Kanban DRY&amp;WET, not still in the warehouse). When none of the feeders
        /// dispatches the product at all (e.g. NEXX-003 has no Ag target feeder), the resource cannot serve the lot.
        /// </summary>
        private Material CreateConsumableFor(BOMProduct bomProduct, string? productName, string resourceName, List<Resource> feeders)
        {
            var productMaterials = mes.Materials.FindByProduct(bomProduct.TargetEntity.Id).Select(m => m.Name).ToHashSet();
            var template = feeders
                .SelectMany(feeder => mes.Setup.GetDispatchList(feeder, new FilterCollection()))
                .FirstOrDefault(m => productMaterials.Contains(m.Name))
                ?? throw new ResourceUnsuitableException($"none of its feeders takes '{productName}'");
            template = tracker.Reload(template);

            var rules = line.Consumables.For(productName);
            logger.LogInformation($"No dispatchable '{productName}': creating one like '{template.Name}' ({rules.ReplacementQuantity})");
            return CreateConsumableLike(template, productName, rules.ReplacementQuantity!.Value);
        }

        /// <summary>
        /// A new material like <paramref name="template"/> (same product, facility, flow path, form, type and units)
        /// with <paramref name="quantity"/>, made dispatchable.
        /// </summary>
        private Material CreateConsumableLike(Material template, string? productName, decimal quantity)
        {
            var newName = $"{productName ?? template.Name}.{Guid.NewGuid().ToString("N")[..8].ToUpperInvariant()}";

            Material created = null!;
            ConsumableStep($"create '{newName}' like '{template.Name}'", () =>
                created = mes.MasterData.Create(new Material()
                {
                    Name = newName,
                    Facility = template.Facility,
                    Product = template.Product,
                    FlowPath = template.FlowPath,
                    Form = template.Form,
                    Type = template.Type,
                    PrimaryQuantity = quantity,
                    PrimaryUnits = template.PrimaryUnits,
                    SecondaryUnits = template.SecondaryUnits
                }) ?? throw new InvalidOperationException($"'{newName}' was not created"));

            ConsumableStep($"set '{newName}' dispatchable", () =>
                created = mes.Materials.SetDispatchable(created));

            return created;
        }

        /// <summary>
        /// Swaps a low consumable on a feeder for a new one: creates a new material like it with the product's
        /// ReplacementQuantity, detaches the original and attaches the new one in one ManageResourceConsumableFeeds
        /// call, then terminates the original with the product's TerminateReason.
        /// </summary>
        private void ReplaceConsumable(Resource resource, Material material, Resource feeder, Material consumable, ConsumableRules rules)
        {
            var productName = GetProductName(consumable.Product);
            logger.LogDebug($"Replacing '{consumable.Name}' ({consumable.PrimaryQuantity}) on feeder '{feeder.Name}' with a new one ({rules.ReplacementQuantity})");

            var replacement = CreateConsumableLike(consumable, productName, rules.ReplacementQuantity!.Value);

            // Same call as the setup wizard, detaching the original and attaching the new material together
            ConsumableStep($"swap '{consumable.Name}' for '{replacement.Name}' on feeder '{feeder.Name}'", () =>
                mes.Resources.ManageConsumableFeeds(resource, tracker.Reload(material),
                    consumablesToAttach: new Dictionary<Resource, MaterialCollection>() { { feeder, [tracker.Reload(replacement)] } },
                    consumablesToDetach: new Dictionary<Resource, MaterialCollection>() { { feeder, [tracker.Reload(consumable)] } }));

            ConsumableStep($"terminate '{consumable.Name}' ({rules.TerminateReason})", () =>
                mes.Materials.Terminate(tracker.Reload(consumable), GetReasonByName(rules.TerminateReason!)));

            logger.LogInformation($"Replaced '{consumable.Name}' on feeder '{feeder.Name}' with '{replacement.Name}' ({rules.ReplacementQuantity})");
        }

        /// <summary>
        /// Runs one MES call of a consumable creation or replacement, logging it and naming it in the error when it fails.
        /// </summary>
        private void ConsumableStep(string description, Action call)
        {
            logger.LogDebug($"Consumable: {description}");
            try
            {
                call();
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Consumable failed to {description}: {ex.Message}", ex);
            }
        }

        private Reason GetReasonByName(string reasonName) =>
            _reasons.GetOrAdd(reasonName, name => mes.MasterData.GetByName<Reason>(name)!);

        /// <summary>
        /// BOM products the material consumes on this resource, as the Perform Setup wizard resolves them
        /// (GetDataForPerformSetupWizard + EvaluateBOMProductsCondition against the material characteristics).
        /// </summary>
        private (List<BOMProduct> BomProducts, Dictionary<long, long> BomProductToConsumableFeed) GetConsumableBomProducts(Resource resource, Material material)
        {
            material = tracker.Reload(material);

            var setupInformation = mes.Setup.GetBomSetupInformation(material, resource);

            // BOM product id -> consumable feed id already providing it
            var bomProductToConsumableFeed = setupInformation?.BOMProductToConsumableFeed ?? [];

            var bom = setupInformation?.BOM;
            if (bom == null)
            {
                return ([], bomProductToConsumableFeed);
            }

            var bomProducts = new BOMProductCollection();
            bomProducts.AddRange(bom.RelationCollection != null && bom.RelationCollection.ContainsKey("BOMProduct")
                ? bom.RelationCollection["BOMProduct"].Cast<BOMProduct>()
                : bom.BomProducts?.Cast<BOMProduct>() ?? []);

            if (bomProducts.Count == 0)
            {
                return ([], bomProductToConsumableFeed);
            }

            // Conditions such as 'Coating = "PI" and Quality = "QA"' are resolved from the material characteristics
            var characteristics = mes.Materials.LoadCharacteristics(material).MaterialCharacteristics?
                .ToDictionary(c => c.Name, c => c.Value) ?? [];

            return (mes.Setup.EvaluateBomProductsCondition(bomProducts, characteristics, material), bomProductToConsumableFeed);
        }

        /// <summary>
        /// Consumables the feeder can dispatch for the BOM product, with the same filters the setup wizard uses
        /// (product, BOM product step, units, approved).
        /// </summary>
        private List<Material> GetDispatchableConsumables(Resource feeder, BOMProduct bomProduct)
        {
            // The dispatch list filters ProductId by the product definition (shared by all revisions),
            // not by the revision Id the BOM product points to
            long productDefinitionId = bomProduct.TargetEntity.DefinitionId;
            if (productDefinitionId == 0)
            {
                productDefinitionId = mes.MasterData.GetById<Product>(bomProduct.TargetEntity.Id)!.DefinitionId;
            }

            var filters = new FilterCollection()
            {
                new Filter() { Name = "ProductId", Operator = FieldOperator.In, Value = new[] { productDefinitionId }, LogicalOperator = LogicalOperator.AND },
                new Filter() { Name = "IsApproved", Operator = FieldOperator.IsEqualTo, Value = true, LogicalOperator = LogicalOperator.AND }
            };

            if (bomProduct.Step != null)
            {
                filters.Add(new Filter() { Name = "StepId", Operator = FieldOperator.IsEqualTo, Value = bomProduct.Step.Id, LogicalOperator = LogicalOperator.AND });
            }

            if (!string.IsNullOrEmpty(bomProduct.Units))
            {
                filters.Add(new Filter() { Name = "PrimaryUnits", Operator = FieldOperator.IsEqualTo, Value = bomProduct.Units, LogicalOperator = LogicalOperator.AND });
            }

            return mes.Setup.GetDispatchList(feeder, filters);
        }

        private string? GetProductName(Product? product)
        {
            if (product == null || !string.IsNullOrEmpty(product.Name))
            {
                return product?.Name;
            }

            return mes.MasterData.GetById<Product>(product.Id)?.Name;
        }

        private Resource GetResourceByName(string resourceName) =>
            mes.Resources.GetByName(resourceName) ?? throw new InvalidOperationException($"Resource '{resourceName}' not found");
    }
}
