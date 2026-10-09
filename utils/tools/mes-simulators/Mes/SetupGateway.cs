using Cmf.Foundation.BusinessObjects.QueryObject;
using Cmf.Navigo.BusinessObjects;
using Cmf.Navigo.BusinessOrchestration.DispatchManagement.InputObjects;
using Cmf.Navigo.BusinessOrchestration.FacilityManagement.FlowManagement.InputObjects;
using Cmf.Navigo.BusinessOrchestration.FacilityManagement.ProductManagement.InputObjects;
using Cmf.Navigo.BusinessOrchestration.MaterialManagement.InputObjects;
using Cmf.Navigo.BusinessOrchestration.ResourceManagement.InputObjects;
using Cmf.Navigo.BusinessOrchestration.ResourceManagement.OutputObjects;

namespace MESSimulator.Mes
{
    /// <summary>
    /// The calls behind the UI "Perform Setup" wizard: BOM setup data, BOM conditions and feeder dispatch lists.
    /// </summary>
    public interface ISetupGateway
    {
        BOMSetupInformation? GetBomSetupInformation(Material material, Resource resource);
        List<BOMProduct> EvaluateBomProductsCondition(BOMProductCollection bomProducts, Dictionary<string, string> characteristics, Material material);
        List<Material> GetDispatchList(Resource resource, FilterCollection filters, int levelsToLoad = 1);

        /// <summary>The line flow the material runs at its current (line) step, e.g. Clean-Cu-SRD at METAL PLATING.</summary>
        Flow? ResolveLineFlow(Material material);

        /// <summary>The steps of a flow, in order.</summary>
        List<Step> GetFlowSteps(Flow flow);

        /// <summary>Resources that can run the step.</summary>
        List<Resource> GetResourcesForStep(Step step);

        /// <summary>
        /// For a material in a line step: the resources (chambers of its lane) each line flow step may run on, by line
        /// step name. Only these are accepted at the wafers' track-in.
        /// </summary>
        Dictionary<string, List<Resource>> GetLineStepResources(Material material);
    }

    public sealed class SetupGateway(IMesCall mes) : ISetupGateway
    {
        public BOMSetupInformation? GetBomSetupInformation(Material material, Resource resource) =>
            mes.Run("GetDataForPerformSetupWizard", () => new GetDataForPerformSetupWizardInput()
            {
                Material = material,
                Resource = resource,
                IgnoreLastServiceId = true
            }.GetDataForPerformSetupWizardSync(), $"{material.Name} @ {resource.Name}").BOMSetupInformation;

        public List<BOMProduct> EvaluateBomProductsCondition(BOMProductCollection bomProducts, Dictionary<string, string> characteristics, Material material) =>
            mes.Run("EvaluateBOMProductsCondition", () => new EvaluateBOMProductsConditionInput()
            {
                BOMProducts = bomProducts,
                Characteristics = characteristics,
                Material = material
            }.EvaluateBOMProductsConditionSync(), material.Name).ValidBOMProducts.Cast<BOMProduct>().ToList();

        public Flow? ResolveLineFlow(Material material) =>
            mes.Run("ResolveStepLineFlowForMaterial", () => new ResolveStepLineFlowForMaterialInput()
            {
                Material = material
            }.ResolveStepLineFlowForMaterialSync(), material.Name).LineFlow;

        public List<Step> GetFlowSteps(Flow flow)
        {
            var loaded = mes.Run("LoadFlowChilds", () => new LoadFlowChildsInput()
            {
                Flow = flow,
                LevelsToLoad = 1
            }.LoadFlowChildsSync(), flow.Name).Flow;

            return (loaded.FlowItems ?? []).Where(i => i.Step != null).OrderBy(i => i.Position).Select(i => i.Step).ToList();
        }

        public List<Resource> GetResourcesForStep(Step step) =>
            mes.Run("GetResourcesForStep", () => new GetResourcesForStepInput()
            {
                Step = step
            }.GetResourcesForStepSync(), step.Name).ResourceCollection?.Cast<Resource>().ToList() ?? [];

        public Dictionary<string, List<Resource>> GetLineStepResources(Material material)
        {
            var lineSteps = mes.Run("GetMaterialLineData", () => new GetMaterialLineDataInput()
            {
                Material = material,
                IsToLoadResourcesData = true
            }.GetMaterialLineDataSync(), material.Name).LineStepsData ?? [];

            return lineSteps.ToDictionary(
                s => s.Step.Name,
                s => s.LineResourcesData?.Select(r => r.Resource).ToList() ?? [],
                StringComparer.OrdinalIgnoreCase);
        }

        public List<Material> GetDispatchList(Resource resource, FilterCollection filters, int levelsToLoad = 1) =>
            mes.Run("GetDispatchListForResource", () => new GetDispatchListForResourceInput()
            {
                Resource = resource,
                LevelsToLoad = levelsToLoad,
                Filters = filters
            }.GetDispatchListForResourceSync(), resource.Name).Materials?.Cast<Material>().ToList() ?? [];
    }
}
