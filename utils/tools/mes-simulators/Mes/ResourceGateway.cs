using Cmf.Foundation.BusinessObjects;
using Cmf.Foundation.BusinessOrchestration.GenericServiceManagement.InputObjects;
using Cmf.Navigo.BusinessObjects;
using Cmf.Navigo.BusinessOrchestration.ResourceManagement.InputObjects;
using Cmf.Navigo.BusinessOrchestration.ResourceManagement.OutputObjects;

namespace MESSimulator.Mes
{
    /// <summary>
    /// Resource operations: load, state events, consumable feeds and durables.
    /// </summary>
    public interface IResourceGateway
    {
        Resource? GetByName(string name);
        Resource? GetById(long id);
        Resource LogEvent(Resource resource, string reason, StateModel stateModel, StateModelTransition? transition);
        void ManageConsumableFeeds(Resource resource, Material material,
            Dictionary<Resource, MaterialCollection>? consumablesToAttach,
            Dictionary<Resource, MaterialCollection>? consumablesToDetach = null);
        GetDataToManageDurablesOutput GetDurablesData(Material material, Resource resource);

        /// <summary>The resource's lanes (multi-lane resources, e.g. the Semitool Raider plating line).</summary>
        ResourceLaneCollection GetLanes(Resource resource);
        void ManageDurables(Material material, Resource resource, MaterialCollection? durablesToDetach, ResourceDurableCollection? durablesToAttach);
    }

    public sealed class ResourceGateway(IMesCall mes) : IResourceGateway
    {
        public Resource? GetByName(string name) =>
            mes.Run("GetObjectByName", () => new GetObjectByNameInput()
            {
                Name = name,
                Type = typeof(Resource),
                IgnoreLastServiceId = true
            }.GetObjectByNameSync(), name).Instance as Resource;

        public Resource? GetById(long id) =>
            mes.Run("GetObjectById", () => new GetObjectByIdInput()
            {
                Id = id,
                Type = typeof(Resource),
                IgnoreLastServiceId = true
            }.GetObjectByIdSync(), $"Resource {id}").Instance as Resource;

        public Resource LogEvent(Resource resource, string reason, StateModel stateModel, StateModelTransition? transition) =>
            mes.Run("ComplexLogResourceEvent", () => new ComplexLogResourceEventInput()
            {
                Resource = resource,
                Reason = reason,
                StateModel = stateModel,
                StateModelTransition = transition,
                IgnoreLastServiceId = true
            }.ComplexLogResourceEventSync(), $"{resource.Name} ({reason})").Resource;

        public void ManageConsumableFeeds(Resource resource, Material material,
            Dictionary<Resource, MaterialCollection>? consumablesToAttach,
            Dictionary<Resource, MaterialCollection>? consumablesToDetach = null) =>
            mes.Run("ManageResourceConsumableFeeds", () => new ManageResourceConsumableFeedsInput()
            {
                Resource = resource,
                Material = material,
                ConsumablesToAttach = consumablesToAttach,
                ConsumablesToDetach = consumablesToDetach
            }.ManageResourceConsumableFeedsSync(), $"{resource.Name} for {material.Name}");

        public ResourceLaneCollection GetLanes(Resource resource) =>
            mes.Run("LoadResourceLanesFromResource", () => new LoadResourceLanesFromResourceInput()
            {
                Resource = resource
            }.LoadResourceLanesFromResourceSync(), resource.Name).ResourceLanes ?? [];

        public GetDataToManageDurablesOutput GetDurablesData(Material material, Resource resource) =>
            mes.Run("GetDataToManageDurables", () => new GetDataToManageDurablesInput()
            {
                Material = material,
                Resource = resource
            }.GetDataToManageDurablesSync(), $"{material.Name} @ {resource.Name}");

        public void ManageDurables(Material material, Resource resource, MaterialCollection? durablesToDetach, ResourceDurableCollection? durablesToAttach) =>
            mes.Run("ManageResourceMaterialDurables", () => new ManageResourceMaterialDurablesInput()
            {
                Material = material,
                Resource = resource,
                ManageDurables = new ManageResourceDurablesInput()
                {
                    DurableToDetachCollection = durablesToDetach,
                    DurableToAttachCollection = durablesToAttach
                }
            }.ManageResourceMaterialDurablesSync(), $"{material.Name} @ {resource.Name}");
    }
}
