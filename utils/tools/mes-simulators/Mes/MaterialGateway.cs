using Cmf.Foundation.BusinessObjects;
using Cmf.Foundation.BusinessObjects.QueryObject;
using Cmf.Foundation.BusinessOrchestration.GenericServiceManagement.InputObjects;
using Cmf.Foundation.BusinessOrchestration.QueryManagement.InputObjects;
using Cmf.Foundation.Common;
using Cmf.Navigo.BusinessObjects;
using Cmf.Navigo.BusinessOrchestration.EdcManagement.DataCollectionManagement.InputObjects;
using Cmf.Navigo.BusinessOrchestration.MaterialLogisticsManagement.InputObjects;
using Cmf.Navigo.BusinessOrchestration.MaterialManagement.InputObjects;

namespace MESSimulator.Mes
{
    /// <summary>
    /// Material operations: load, move, compose, track in/out, split, abort, quantity and terminate.
    /// </summary>
    public interface IMaterialGateway
    {
        Material? GetByName(string name);
        MaterialCollection GetByNames(IEnumerable<string> names, int levelsToLoad = 1);
        Material SetDispatchable(Material material);
        Material MoveToNextStep(Material material, string flowPath);
        MaterialCollection MoveToNextStep(IReadOnlyDictionary<Material, string> materialFlowPaths);
        Material Compose(Material material, ComposeSourceMaterialCollection sourceMaterials);

        /// <summary>
        /// Skips the processing of the queued lots' step: they become processed without a track-in, so they can be moved
        /// next (the move-next wizard refuses a queued lot).
        /// </summary>
        MaterialCollection SkipProcess(MaterialCollection materials);
        Material LoadChildren(Material material);
        Material LoadCharacteristics(Material material);
        string? GetTrackOutAndMoveNextFlowPath(Material material);
        string? GetMoveNextFlowPath(Material material);
        /// <summary>Dispatches and tracks in; <paramref name="lane"/> is required on resources with multiple lanes active.</summary>
        Material DispatchAndTrackIn(Material material, Resource resource, StateModel? stateModel, StateModelTransition? transition, ResourceLane? lane = null);
        MaterialCollection TrackIn(MaterialCollection materials, StateModel? stateModel);

        /// <summary>Tracks in on <paramref name="resource"/> materials that are already dispatched (e.g. wafers in a line flow onto a chamber).</summary>
        MaterialCollection TrackIn(MaterialCollection materials, Resource resource, StateModel? stateModel, StateModelTransition? transition);
        MaterialCollection TrackOutAndMoveNext(MaterialCollection materials, string flowPath, StateModel? stateModel, StateModelTransition? transition);
        MaterialCollection TrackOut(MaterialCollection materials, StateModel? stateModel, StateModelTransition? transition);
        Material TrackOutAndMoveNext(Material material, string flowPath, StateModel? stateModel, StateModelTransition? transition);
        Material TrackOut(Dictionary<Material, ComplexTrackOutParameters> materials, StateModel? stateModel, StateModelTransition? transition);
        void AbortProcess(MaterialCollection materials);
        Material ChangeQuantity(Material material, decimal newPrimaryQuantity);
        void Terminate(Material material, Reason? lossReason);
        void Terminate(MaterialCollection materials, Reason? lossReason);

        /// <summary>Ships the materials to another facility (e.g. SORTER in Production FE SC to Wafer PACKING in Warehouse FE SC).</summary>
        void Ship(MaterialCollection materials, Facility destination);

        /// <summary>Receives a shipped (in transit) material at the destination facility, at <paramref name="flowPath"/>.</summary>
        Material Receive(Material material, string flowPath);

        /// <summary>The rework paths the MES offers for the material at its current step.</summary>
        List<ReworkPath> GetPossibleReworkPaths(Material material);

        /// <summary>Sends the material to the rework path's flow (it returns to the path's return step afterwards).</summary>
        Material Rework(Material material, ReworkPath path);

        /// <summary>Scraps the lot's <paramref name="wafers"/> (one unit each) for <paramref name="reason"/>; the lot terminates at zero.</summary>
        void RecordWaferLosses(Material lot, IReadOnlyList<Material> wafers, Reason reason);

        /// <summary>What packing the material still needs at its step (quantity to pack, mode, packable sub-materials).</summary>
        MaterialPackingInformation GetPackingInformation(Material material);

        /// <summary>Packs the sub-materials (by id) of the material into a new package; returns the package.</summary>
        Package Pack(Material material, MaterialCollection subMaterials);

        /// <summary>Reads the lot with its basic information (including its current BOM instance) loaded.</summary>
        Material LoadBasicInformation(Material material);

        /// <summary>The lot's current data collection, with its parameters (and reading and sample names) loaded.</summary>
        DataCollection LoadDataCollectionParameters(Material material);

        /// <summary>Posts the points to the data collection instance; returns the updated material.</summary>
        Material PostDataCollectionPoints(DataCollectionInstance instance, DataCollectionPointCollection points);

        /// <summary>The source materials (sub-lots and consumables) the MES resolves for assembling the lot, keyed by BOM product.</summary>
        IReadOnlyList<AssembleSourceMaterial> GetBomMaterialsForAssemble(Material material);

        /// <summary>The BOM instance items (required products and quantities) of a BOM instance.</summary>
        IReadOnlyList<BOMInstanceItem> GetBomInstanceItems(BOMInstance bomInstance);

        /// <summary>Assembles the lot from its BOM: consumes the source materials into the lot (mixed sub-lots + consumables).</summary>
        Material Assemble(Material material, AssembleMaterialCollection sourceMaterials, BOMProductInAssembleCollection bomProductsInAssemble);

        /// <summary>Packs one unit of the lot's quantity into a package (RTU-style whole-lot packing, no sub-materials). Returns the updated lot.</summary>
        Material PackLot(Material material);

        /// <summary>The multi-level packing levels (package product and capacities) for palletizing the material.</summary>
        IReadOnlyList<MultiLevelPackingInformation> GetMultiLevelPackingInformation(Material material);

        /// <summary>The packages currently holding the material.</summary>
        List<Package> GetMaterialPackages(long materialId);

        /// <summary>Creates empty parent packages from the given parameters (palletizing); returns the new packages.</summary>
        PackageCollection CreatePackages(CreatePackageParametersCollection parameters);

        /// <summary>Nests the child packages inside the parent package.</summary>
        void AddPackagesToPackage(Package parentPackage, PackageCollection childPackages, Material material);

        /// <summary>Closes the packages.</summary>
        void ClosePackages(Material material, PackageCollection packages);

        /// <summary>Materials not terminated (with their production order) whose production order name starts with <paramref name="productionOrderPrefix"/>.</summary>
        List<(string Material, string ProductionOrder)> FindOpenByProductionOrder(string productionOrderPrefix);

        /// <summary>Active materials of the product (any revision), optionally only in <paramref name="systemState"/>.</summary>
        List<Material> FindByProduct(long productId, MaterialSystemState? systemState = null);

        /// <summary>
        /// Primary quantity of the product's materials queued at <paramref name="stepName"/> in <paramref name="facilityName"/>,
        /// not on hold (the stock an assembly can consume).
        /// </summary>
        decimal StockAt(long productId, string facilityName, string stepName);
    }

    /// <summary>One source material the MES can consume to assemble a lot (a sub-lot or consumable).</summary>
    public sealed record AssembleSourceMaterial(long BOMProductId, long MaterialId, string MaterialName, decimal PrimaryQuantity);

    public sealed class MaterialGateway(IMesCall mes) : IMaterialGateway
    {
        public Material? GetByName(string name) =>
            mes.Run("GetObjectByName", () => new GetObjectByNameInput()
            {
                Name = name,
                Type = typeof(Material),
                IgnoreLastServiceId = true
            }.GetObjectByNameSync(), name).Instance as Material;

        public MaterialCollection GetByNames(IEnumerable<string> names, int levelsToLoad = 1)
        {
            var nameList = names.ToList();
            var materials = mes.Run("GetObjectsByFilter", () => new GetObjectsByFilterInput()
            {
                Type = new Material(),
                LevelsToLoad = levelsToLoad,
                Filter = [new Filter() { Name = "Name", Operator = FieldOperator.In, Value = nameList }]
            }.GetObjectsByFilterSync(), $"{nameList.Count} material(s)").Instance.Cast<Material>();

            var collection = new MaterialCollection();
            collection.AddRange(materials);
            return collection;
        }

        public Material SetDispatchable(Material material) =>
            mes.Run("SetOrUnSetMaterialDispatchable", () => new SetOrUnSetMaterialDispatchableInput()
            {
                Material = material,
                ExecuteRule = true,
                IgnoreLastServiceId = true,
                IsToOverrideCurrentSetService = false
            }.SetOrUnSetMaterialDispatchableSync(), material.Name).Material;

        public Material MoveToNextStep(Material material, string flowPath) =>
            mes.Run("MoveMaterialToNextStep", () => new MoveMaterialToNextStepInput()
            {
                Material = material,
                FlowPath = flowPath
            }.MoveMaterialToNextStepSync(), material.Name).Material;

        public MaterialCollection MoveToNextStep(IReadOnlyDictionary<Material, string> materialFlowPaths) =>
            mes.Run("ComplexMoveMaterialsToNextStep", () => new ComplexMoveMaterialsToNextStepInput()
            {
                Materials = materialFlowPaths.ToDictionary(),
                IgnoreLastServiceId = true
            }.ComplexMoveMaterialsToNextStepSync(), $"{materialFlowPaths.Count} material(s)").Materials;

        public Material Compose(Material material, ComposeSourceMaterialCollection sourceMaterials) =>
            mes.Run("ComposeMaterial", () => new ComposeMaterialInput()
            {
                Mode = ComposeMode.SubMaterials,
                Material = material,
                ComposeSourceMaterials = sourceMaterials
            }.ComposeMaterialSync(), material.Name).Material;

        public MaterialCollection SkipProcess(MaterialCollection materials) =>
            mes.Run("SkipMaterialsProcess", () => new SkipMaterialsProcessInput()
            {
                Materials = materials
            }.SkipMaterialsProcessSync(), $"{materials.Count} material(s)").Materials;

        public Material LoadChildren(Material material) =>
            mes.Run("LoadMaterialChildren", () => new LoadMaterialChildrenInput()
            {
                Material = material
            }.LoadMaterialChildrenSync(), material.Name).Material;

        public Material LoadCharacteristics(Material material) =>
            mes.Run("LoadMaterialCharacteristics", () => new LoadMaterialCharacteristicsInput()
            {
                Material = material
            }.LoadMaterialCharacteristicsSync(), material.Name).Material;

        public string? GetTrackOutAndMoveNextFlowPath(Material material) =>
            mes.Run("GetDataForMultipleTrackOutAndMoveNextWizard", () => new GetDataForMultipleTrackOutAndMoveNextWizardInput()
            {
                Operation = GetDataForTrackOutAndMoveNextOperation.TrackOutAndMoveNext,
                Materials = [material]
            }.GetDataForMultipleTrackOutAndMoveNextWizardSync(), material.Name).NextStepsResults.FirstOrDefault()?.FlowPath;

        public string? GetMoveNextFlowPath(Material material) =>
            mes.Run("GetDataForMultipleMoveNextWizard", () => new GetDataForMultipleMoveNextWizardInput()
            {
                Materials = [material]
            }.GetDataForMultipleMoveNextWizardSync(), material.Name).NextStepsResults.FirstOrDefault()?.FlowPath;

        public Material DispatchAndTrackIn(Material material, Resource resource, StateModel? stateModel, StateModelTransition? transition, ResourceLane? lane = null) =>
            mes.Run("ComplexDispatchAndTrackInMaterials", () => new ComplexDispatchAndTrackInMaterialsInput()
            {
                MaterialCollection = new Dictionary<Material, DispatchMaterialParameters>()
                {
                    { material, new DispatchMaterialParameters() { Resource = resource, Lane = lane } }
                },
                StateModel = stateModel,
                StateModelTransition = transition,
                IgnoreLastServiceId = true
            }.ComplexDispatchAndTrackInMaterialsSync(), $"{material.Name} @ {resource.Name}").Materials.First();

        public MaterialCollection TrackIn(MaterialCollection materials, Resource resource, StateModel? stateModel, StateModelTransition? transition) =>
            mes.Run("ComplexTrackInMaterials", () => new ComplexTrackInMaterialsInput()
            {
                Materials = materials,
                Resource = resource,
                StateModel = stateModel,
                StateModelTransition = transition,
                IgnoreLastServiceId = true
            }.ComplexTrackInMaterialsSync(), $"{materials.Count} material(s) @ {resource.Name}").Materials;

        public MaterialCollection TrackOutAndMoveNext(MaterialCollection materials, string flowPath, StateModel? stateModel, StateModelTransition? transition)
        {
            var output = mes.Run("ComplexTrackOutAndMoveMaterialsToNextStep", () => new ComplexTrackOutAndMoveMaterialsToNextStepInput()
            {
                Materials = materials.ToDictionary(m => m, _ => new ComplexTrackOutAndMoveNextParameters() { FlowPath = flowPath }),
                StateModel = stateModel,
                StateModelTransition = transition,
                IgnoreLastServiceId = true
            }.ComplexTrackOutAndMoveMaterialsToNextStepSync(), $"{materials.Count} material(s)").Materials;

            var collection = new MaterialCollection();
            collection.AddRange(output.Keys);
            return collection;
        }

        public MaterialCollection TrackOut(MaterialCollection materials, StateModel? stateModel, StateModelTransition? transition)
        {
            var output = mes.Run("ComplexTrackOutMaterials", () => new ComplexTrackOutMaterialsInput()
            {
                Material = materials.ToDictionary(m => m, _ => new ComplexTrackOutParameters()),
                StateModel = stateModel,
                StateModelTransition = transition,
                IgnoreLastServiceId = true
            }.ComplexTrackOutMaterialsSync(), $"{materials.Count} material(s)").Materials;

            var collection = new MaterialCollection();
            collection.AddRange(output.Keys);
            return collection;
        }

        public MaterialCollection TrackIn(MaterialCollection materials, StateModel? stateModel) =>
            mes.Run("ComplexTrackInMaterials", () => new ComplexTrackInMaterialsInput()
            {
                Materials = materials,
                StateModel = stateModel,
                IgnoreLastServiceId = true
            }.ComplexTrackInMaterialsSync(), $"{materials.Count} material(s)").Materials;

        public Material TrackOutAndMoveNext(Material material, string flowPath, StateModel? stateModel, StateModelTransition? transition) =>
            mes.Run("ComplexTrackOutAndMoveMaterialsToNextStep", () => new ComplexTrackOutAndMoveMaterialsToNextStepInput()
            {
                Materials = new Dictionary<Material, ComplexTrackOutAndMoveNextParameters>()
                {
                    { material, new ComplexTrackOutAndMoveNextParameters() { FlowPath = flowPath } }
                },
                StateModel = stateModel,
                StateModelTransition = transition,
                IgnoreLastServiceId = true
            }.ComplexTrackOutAndMoveMaterialsToNextStepSync(), material.Name).Materials.First().Key;

        public Material TrackOut(Dictionary<Material, ComplexTrackOutParameters> materials, StateModel? stateModel, StateModelTransition? transition) =>
            mes.Run("ComplexTrackOutMaterials", () => new ComplexTrackOutMaterialsInput()
            {
                Material = materials,
                StateModel = stateModel,
                StateModelTransition = transition,
                IgnoreLastServiceId = true
            }.ComplexTrackOutMaterialsSync(), string.Join(",", materials.Keys.Select(m => m.Name))).Materials.First().Key;

        public void AbortProcess(MaterialCollection materials) =>
            mes.Run("AbortMaterialsProcess", () => new AbortMaterialsProcessInput()
            {
                Materials = materials,
                IgnoreLastServiceId = true
            }.AbortMaterialsProcessSync(), string.Join(",", materials.Select(m => m.Name)));

        public Material ChangeQuantity(Material material, decimal newPrimaryQuantity) =>
            mes.Run("ChangeMaterialQuantity", () => new ChangeMaterialQuantityInput()
            {
                MaterialQuantityChange = new MaterialQuantityChange()
                {
                    Material = material,
                    NewPrimaryQuantity = newPrimaryQuantity
                }
            }.ChangeMaterialQuantitySync(), material.Name).Material;

        public void Terminate(Material material, Reason? lossReason) =>
            mes.Run("TerminateMaterial", () => new TerminateMaterialInput()
            {
                Material = material,
                LossReason = lossReason
            }.TerminateMaterialSync(), material.Name);

        public void Ship(MaterialCollection materials, Facility destination) =>
            mes.Run("ShipMaterials", () => new ShipMaterialsInput()
            {
                Materials = materials,
                DestinationFacility = destination
            }.ShipMaterialsSync(), $"{string.Join(",", materials.Select(m => m.Name))} to {destination.Name}");

        public Material Receive(Material material, string flowPath) =>
            mes.Run("ReceiveMaterials", () => new ReceiveMaterialsInput()
            {
                Materials = new Dictionary<Material, ReceiveMaterialParameters>()
                {
                    { material, new ReceiveMaterialParameters() { FlowPath = flowPath } }
                }
            }.ReceiveMaterialsSync(), $"{material.Name} at {flowPath}").Materials.First();

        public List<ReworkPath> GetPossibleReworkPaths(Material material) =>
            mes.Run("GetPossibleReworkPathsForMaterial", () => new GetPossibleReworkPathsForMaterialInput()
            {
                Material = material,
                LevelsToLoad = 1
            }.GetPossibleReworkPathsForMaterialSync(), material.Name).ReworkPathCollection?.Cast<ReworkPath>().ToList() ?? [];

        // Same call as the other simulators (IndustrialEquipmentSimulator): the off-flow built from the rework path
        public Material Rework(Material material, ReworkPath path) =>
            mes.Run("ComplexReworkMaterial", () => new ComplexReworkMaterialInput()
            {
                Material = material,
                MaterialOffFlow = new MaterialOffFlow()
                {
                    Material = material,
                    OffFlowType = OffFlowType.Rework,
                    Reason = path.ReworkReason,
                    GotoFlow = path.GotoFlow,
                    GotoFlowPath = path.GotoFlowPath,
                    GotoStep = path.GotoStep,
                    ReturnFlow = material.Flow,
                    ReturnFlowPath = path.ReturnFlowPath,
                    ReturnStep = path.ReturnStep,
                    ReworkPath = path
                },
                IgnoreLastServiceId = true
            }.ComplexReworkMaterialSync(), $"{material.Name} to {path.GotoFlowPath}").Material;

        public void RecordWaferLosses(Material lot, IReadOnlyList<Material> wafers, Reason reason) =>
            mes.Run("RecordSubMaterialsLoss", () => new RecordSubMaterialsLossInput()
            {
                SubMaterials = wafers.ToDictionary(w => w, w => new LossBonusAffectedQuantityCollection()
                {
                    new LossBonusAffectedQuantity() { Reason = reason, ReasonPrimaryQuantity = w.PrimaryQuantity ?? 1 }
                }),
                TerminateOnZeroQuantity = true,
                IgnoreLastServiceId = true
            }.RecordSubMaterialsLossSync(), $"{wafers.Count} wafer(s) of {lot.Name}");

        public MaterialPackingInformation GetPackingInformation(Material material) =>
            mes.Run("GetPackingInformation", () => new GetPackingInformationInput()
            {
                Material = material
            }.GetPackingInformationSync(), material.Name).MaterialPackingInformation;

        public Package Pack(Material material, MaterialCollection subMaterials) =>
            mes.Run("PackMaterial", () => new PackMaterialInput()
            {
                // By material ids: no package quantity (that is for packing quantities in several packages)
                Parameters = new PackMaterialParameters() { Material = material, SubMaterials = subMaterials }
            }.PackMaterialSync(), $"{material.Name} ({subMaterials.Count})").Package;

        public Material LoadBasicInformation(Material material) =>
            mes.Run("GetMaterialBasicInformation", () => new GetMaterialBasicInformationInput()
            {
                Material = material,
                LevelsToLoad = 2
            }.GetMaterialBasicInformationSync(), material.Name).Material;

        public DataCollection LoadDataCollectionParameters(Material material) =>
            mes.Run("LoadDataCollectionParameterInformation", () => new LoadDataCollectionParameterInformationInput()
            {
                DataCollection = material.CurrentDataCollectionInstance.DataCollection,
                DataCollectionInstance = material.CurrentDataCollectionInstance,
                LevelsToLoad = 1,
                LoadReadingNames = true,
                LoadSampleNames = true,
                Resource = material.LastProcessedResource
            }.LoadDataCollectionParameterInformationSync(), material.Name).DataCollection;

        public Material PostDataCollectionPoints(DataCollectionInstance instance, DataCollectionPointCollection points) =>
            mes.Run("PostDataCollectionPoints", () => new PostDataCollectionPointsInput()
            {
                DataCollectionInstance = instance,
                DataCollectionPoints = points,
                IgnoreLastServiceId = true
            }.PostDataCollectionPointsSync(), $"{points.Count} point(s)").DataCollectionInstance.Material;

        public IReadOnlyList<AssembleSourceMaterial> GetBomMaterialsForAssemble(Material material)
        {
            var output = mes.Run("GetMaterialBomMaterialsForAssemble", () => new GetMaterialBomMaterialsForAssembleInput()
            {
                Material = material,
                RelationNames = ["BOMProduct"],
                Bom = material.CurrentBOMInstance.BOM,
                BOMInstance = material.CurrentBOMInstance,
                RelationsLevelsToLoad = 1
            }.GetMaterialBomMaterialsForAssembleSync(), material.Name);

            // No material in stock for any BOM product: the MES returns no data set at all
            if (output.SourceMaterials == null)
            {
                return [];
            }

            var dataSet = Utilities.ToDataSet(output.SourceMaterials);
            if (dataSet.Tables.Count == 0)
            {
                return [];
            }

            return dataSet.Tables[0].Rows.Cast<System.Data.DataRow>()
                .Select(row => new AssembleSourceMaterial(
                    Convert.ToInt64(row["BOMProductId"]),
                    Convert.ToInt64(row["MaterialId"]),
                    Convert.ToString(row["MaterialName"]) ?? string.Empty,
                    Convert.ToDecimal(row["PrimaryQuantity"])))
                .ToList();
        }

        public IReadOnlyList<BOMInstanceItem> GetBomInstanceItems(BOMInstance bomInstance) =>
            mes.Run("GetBOMInstanceInformation", () => new GetBOMInstanceInformationInput()
            {
                BOMInstance = bomInstance
            }.GetBOMInstanceInformationSync(), bomInstance.Name).BOMInstanceItems?.Cast<BOMInstanceItem>().ToList() ?? [];

        public Material Assemble(Material material, AssembleMaterialCollection sourceMaterials, BOMProductInAssembleCollection bomProductsInAssemble) =>
            mes.Run("AssembleMaterial", () => new AssembleMaterialInput()
            {
                BOMProductsInAssemble = bomProductsInAssemble,
                AssembleQuantity = material.PrimaryQuantity ?? 0,
                Material = material,
                SourceMaterials = sourceMaterials
            }.AssembleMaterialSync(), material.Name).Material;

        public Material PackLot(Material material) =>
            mes.Run("PackMaterial", () => new PackMaterialInput()
            {
                // Whole-lot (quantity) packing: no sub-materials, the MES packs one unit of the lot's quantity
                Parameters = new PackMaterialParameters() { Material = material }
            }.PackMaterialSync(), $"{material.Name} (whole lot)").Material;

        public IReadOnlyList<MultiLevelPackingInformation> GetMultiLevelPackingInformation(Material material) =>
            mes.Run("GetPackingInformation", () => new GetPackingInformationInput()
            {
                IsToLoadMultiLevelPackages = true,
                IsToResolvePackageLevelsForMaterial = true,
                Material = material
            }.GetPackingInformationSync(), material.Name).MultiLevelPackingInformations?.Cast<MultiLevelPackingInformation>().ToList() ?? [];

        public List<Package> GetMaterialPackages(long materialId)
        {
            var query = new QueryObject
            {
                Description = "",
                EntityTypeName = "Package",
                Name = "GetMaterialPackages",
                Query = new Query
                {
                    Distinct = false,
                    Filters =
                    [
                        new Filter()
                        {
                            Name = "Id",
                            ObjectName = "Material",
                            ObjectAlias = "Package_Material_2",
                            Operator = FieldOperator.IsEqualTo,
                            Value = materialId,
                            LogicalOperator = LogicalOperator.Nothing,
                            FilterType = Cmf.Foundation.BusinessObjects.QueryObject.Enums.FilterType.Normal,
                        }
                    ],
                    Fields =
                    [
                        new Field() { Alias = "Id", ObjectName = "Package", ObjectAlias = "Package_1", IsUserAttribute = false, Name = "Id", Position = 0, Sort = FieldSort.NoSort },
                        new Field() { Alias = "Name", ObjectName = "Package", ObjectAlias = "Package_1", IsUserAttribute = false, Name = "Name", Position = 1, Sort = FieldSort.NoSort }
                    ],
                    Relations =
                    [
                        new Relation()
                        {
                            Alias = "",
                            IsRelation = false,
                            Name = "",
                            SourceEntity = "Package",
                            SourceEntityAlias = "Package_1",
                            SourceJoinType = Cmf.Foundation.BusinessObjects.QueryObject.Enums.JoinType.InnerJoin,
                            SourceProperty = "MaterialId",
                            TargetEntity = "Material",
                            TargetEntityAlias = "Package_Material_2",
                            TargetJoinType = Cmf.Foundation.BusinessObjects.QueryObject.Enums.JoinType.InnerJoin,
                            TargetProperty = "Id"
                        }
                    ]
                }
            };

            var dataSet = Utilities.ToDataSet(mes.Run("ExecuteQuery", () => new ExecuteQueryInput()
            {
                QueryObject = query
            }.ExecuteQuerySync(), $"packages of material {materialId}").NgpDataSet);

            if (dataSet.Tables.Count == 0)
            {
                return [];
            }

            return dataSet.Tables[0].Rows.Cast<System.Data.DataRow>()
                .Select(row => new GetObjectByNameInput()
                {
                    Name = (string)row["Name"],
                    Type = typeof(Package)
                }.GetObjectByNameSync().Instance as Package)
                .Where(p => p != null)
                .Cast<Package>()
                .ToList();
        }

        public PackageCollection CreatePackages(CreatePackageParametersCollection parameters) =>
            mes.Run("CreatePackages", () => new CreatePackagesInput()
            {
                CreatePackagesParametersCollection = parameters
            }.CreatePackagesSync(), $"{parameters.Count} package(s)").Packages;

        public void AddPackagesToPackage(Package parentPackage, PackageCollection childPackages, Material material) =>
            mes.Run("AddPackagesToPackage", () => new AddPackagesToPackageInput()
            {
                ChildPackages = childPackages,
                Material = material,
                ParentPackage = parentPackage,
                IgnoreLastServiceId = true
            }.AddPackagesToPackageSync(), $"{childPackages.Count} package(s) into '{parentPackage.Name}'");

        public void ClosePackages(Material material, PackageCollection packages) =>
            mes.Run("ClosePackages", () => new ClosePackagesInput()
            {
                Material = material,
                PackagesToClose = packages
            }.ClosePackagesSync(), $"{packages.Count} package(s) of {material.Name}");

        public void Terminate(MaterialCollection materials, Reason? lossReason) =>
            mes.Run("TerminateMaterials", () => new TerminateMaterialsInput()
            {
                Materials = materials,
                LossReason = lossReason
            }.TerminateMaterialsSync(), $"{materials.Count} material(s)");

        public List<(string Material, string ProductionOrder)> FindOpenByProductionOrder(string productionOrderPrefix)
        {
            var query = new QueryObject
            {
                Description = "",
                EntityTypeName = "Material",
                Name = "MaterialsOfSimulatorProductionOrders",
                Query = new Query
                {
                    Distinct = false,
                    Filters =
                    [
                        new Filter()
                        {
                            Name = "UniversalState",
                            ObjectName = "Material",
                            ObjectAlias = "Material_1",
                            Operator = FieldOperator.IsNotEqualTo,
                            Value = Cmf.Foundation.Common.Base.UniversalState.Terminated,
                            LogicalOperator = LogicalOperator.AND,
                            FilterType = Cmf.Foundation.BusinessObjects.QueryObject.Enums.FilterType.Normal,
                        },
                        new Filter()
                        {
                            Name = "Name",
                            ObjectName = "ProductionOrder",
                            ObjectAlias = "Material_ProductionOrder_2",
                            Operator = FieldOperator.StartsWith,
                            Value = productionOrderPrefix,
                            LogicalOperator = LogicalOperator.Nothing,
                            FilterType = Cmf.Foundation.BusinessObjects.QueryObject.Enums.FilterType.Normal,
                        }
                    ],
                    Fields =
                    [
                        new Field() { Alias = "Id", ObjectName = "Material", ObjectAlias = "Material_1", IsUserAttribute = false, Name = "Id", Position = 0, Sort = FieldSort.NoSort },
                        new Field() { Alias = "Name", ObjectName = "Material", ObjectAlias = "Material_1", IsUserAttribute = false, Name = "Name", Position = 1, Sort = FieldSort.NoSort },
                        new Field() { Alias = "ProductionOrderName", ObjectName = "ProductionOrder", ObjectAlias = "Material_ProductionOrder_2", IsUserAttribute = false, Name = "Name", Position = 2, Sort = FieldSort.NoSort }
                    ],
                    Relations =
                    [
                        new Relation()
                        {
                            Alias = "",
                            IsRelation = false,
                            Name = "",
                            SourceEntity = "Material",
                            SourceEntityAlias = "Material_1",
                            SourceJoinType = Cmf.Foundation.BusinessObjects.QueryObject.Enums.JoinType.InnerJoin,
                            SourceProperty = "ProductionOrderId",
                            TargetEntity = "ProductionOrder",
                            TargetEntityAlias = "Material_ProductionOrder_2",
                            TargetJoinType = Cmf.Foundation.BusinessObjects.QueryObject.Enums.JoinType.InnerJoin,
                            TargetProperty = "Id"
                        }
                    ]
                }
            };

            var dataSet = Utilities.ToDataSet(mes.Run("ExecuteQuery", () => new ExecuteQueryInput()
            {
                QueryObject = query
            }.ExecuteQuerySync(), $"materials of production orders '{productionOrderPrefix}*'").NgpDataSet);

            return dataSet.Tables.Count > 0
                ? dataSet.Tables[0].Rows.Cast<System.Data.DataRow>().Select(row => ((string)row["Name"], (string)row["ProductionOrderName"])).ToList()
                : [];
        }

        public List<Material> FindByProduct(long productId, MaterialSystemState? systemState = null)
        {
            var filters = new FilterCollection()
            {
                new Filter()
                {
                    Name = "Id",
                    ObjectName = "Product",
                    ObjectAlias = "Material_Product_2",
                    Operator = FieldOperator.IsEqualTo,
                    Value = productId,
                    LogicalOperator = LogicalOperator.AND,
                    FilterType = Cmf.Foundation.BusinessObjects.QueryObject.Enums.FilterType.Normal,
                },
                new Filter()
                {
                    Name = "UniversalState",
                    ObjectName = "Material",
                    ObjectAlias = "Material_1",
                    Operator = FieldOperator.IsEqualTo,
                    Value = Cmf.Foundation.Common.Base.UniversalState.Active,
                    // As in the original durables query, which the MES accepts with a SystemState filter after it
                    LogicalOperator = LogicalOperator.Nothing,
                    FilterType = Cmf.Foundation.BusinessObjects.QueryObject.Enums.FilterType.Normal,
                }
            };
            if (systemState != null)
            {
                filters.Add(new Filter()
                {
                    Name = "SystemState",
                    ObjectName = "Material",
                    ObjectAlias = "Material_1",
                    Operator = FieldOperator.IsEqualTo,
                    Value = systemState.Value,
                    LogicalOperator = LogicalOperator.Nothing,
                    FilterType = Cmf.Foundation.BusinessObjects.QueryObject.Enums.FilterType.Normal,
                });
            }

            var query = new QueryObject
            {
                Description = "",
                EntityTypeName = "Material",
                Name = "GetMaterialForProduct",
                Query = new Query
                {
                    Distinct = false,
                    Filters = filters,
                    Fields =
                    [
                        new Field() { Alias = "Id", ObjectName = "Material", ObjectAlias = "Material_1", IsUserAttribute = false, Name = "Id", Position = 0, Sort = FieldSort.NoSort },
                        new Field() { Alias = "Name", ObjectName = "Material", ObjectAlias = "Material_1", IsUserAttribute = false, Name = "Name", Position = 1, Sort = FieldSort.NoSort }
                    ],
                    // Joined on the product definition, so any revision of the product matches
                    Relations =
                    [
                        new Relation()
                        {
                            Alias = "",
                            IsRelation = false,
                            Name = "",
                            SourceEntity = "Material",
                            SourceEntityAlias = "Material_1",
                            SourceJoinType = Cmf.Foundation.BusinessObjects.QueryObject.Enums.JoinType.InnerJoin,
                            SourceProperty = "ProductId",
                            TargetEntity = "Product",
                            TargetEntityAlias = "Material_Product_2",
                            TargetJoinType = Cmf.Foundation.BusinessObjects.QueryObject.Enums.JoinType.InnerJoin,
                            TargetProperty = "DefinitionId"
                        }
                    ]
                }
            };

            var dataSet = Utilities.ToDataSet(mes.Run("ExecuteQuery", () => new ExecuteQueryInput()
            {
                QueryObject = query
            }.ExecuteQuerySync(), $"materials of product {productId}").NgpDataSet);

            var materials = new List<Material>();
            if (dataSet.Tables.Count > 0)
            {
                foreach (System.Data.DataRow row in dataSet.Tables[0].Rows)
                {
                    var material = GetByName((string)row["Name"]);
                    if (material != null)
                    {
                        materials.Add(material);
                    }
                }
            }
            return materials;
        }

        public decimal StockAt(long productId, string facilityName, string stepName)
        {
            static Filter Equal(string objectName, string alias, string name, object value, LogicalOperator next) => new()
            {
                Name = name,
                ObjectName = objectName,
                ObjectAlias = alias,
                Operator = FieldOperator.IsEqualTo,
                Value = value,
                LogicalOperator = next,
                FilterType = Cmf.Foundation.BusinessObjects.QueryObject.Enums.FilterType.Normal,
            };

            static Relation Join(string sourceProperty, string target, string targetAlias, string targetProperty) => new()
            {
                Alias = "",
                IsRelation = false,
                Name = "",
                SourceEntity = "Material",
                SourceEntityAlias = "Material_1",
                SourceJoinType = Cmf.Foundation.BusinessObjects.QueryObject.Enums.JoinType.InnerJoin,
                SourceProperty = sourceProperty,
                TargetEntity = target,
                TargetEntityAlias = targetAlias,
                TargetJoinType = Cmf.Foundation.BusinessObjects.QueryObject.Enums.JoinType.InnerJoin,
                TargetProperty = targetProperty
            };

            var query = new QueryObject
            {
                Description = "",
                EntityTypeName = "Material",
                Name = "GetMaterialStockAtStep",
                Query = new Query
                {
                    Distinct = false,
                    Filters =
                    [
                        Equal("Product", "Material_Product_2", "Id", productId, LogicalOperator.AND),
                        Equal("Material", "Material_1", "UniversalState", Cmf.Foundation.Common.Base.UniversalState.Active, LogicalOperator.AND),
                        Equal("Material", "Material_1", "SystemState", MaterialSystemState.Queued, LogicalOperator.AND),
                        Equal("Material", "Material_1", "HoldCount", 0, LogicalOperator.AND),
                        Equal("Step", "Material_Step_3", "Name", stepName, LogicalOperator.AND),
                        Equal("Facility", "Material_Facility_4", "Name", facilityName, LogicalOperator.Nothing)
                    ],
                    Fields =
                    [
                        new Field() { Alias = "Name", ObjectName = "Material", ObjectAlias = "Material_1", IsUserAttribute = false, Name = "Name", Position = 0, Sort = FieldSort.NoSort },
                        new Field() { Alias = "PrimaryQuantity", ObjectName = "Material", ObjectAlias = "Material_1", IsUserAttribute = false, Name = "PrimaryQuantity", Position = 1, Sort = FieldSort.NoSort }
                    ],
                    // Joined on the product definition, so any revision of the product matches
                    Relations =
                    [
                        Join("ProductId", "Product", "Material_Product_2", "DefinitionId"),
                        Join("StepId", "Step", "Material_Step_3", "Id"),
                        Join("FacilityId", "Facility", "Material_Facility_4", "Id")
                    ]
                }
            };

            var dataSet = Utilities.ToDataSet(mes.Run("ExecuteQuery", () => new ExecuteQueryInput()
            {
                QueryObject = query
            }.ExecuteQuerySync(), $"stock of product {productId} at {stepName}").NgpDataSet);

            return dataSet.Tables.Count > 0
                ? dataSet.Tables[0].Rows.Cast<System.Data.DataRow>().Sum(row => row["PrimaryQuantity"] is DBNull ? 0m : Convert.ToDecimal(row["PrimaryQuantity"]))
                : 0m;
        }
    }
}
