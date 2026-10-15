using Cmf.Custom.Common.SD.Utilities.Extensions;
using Cmf.Custom.Common.SD.Utilities.Helpers.Abstractions;
using Cmf.Foundation.BusinessObjects.Abstractions;
using Cmf.Foundation.BusinessObjects.QueryObject;
using Cmf.Foundation.BusinessOrchestration.Abstractions;
using Cmf.Foundation.BusinessOrchestration.GenericServiceManagement.InputObjects;
using Cmf.Foundation.Common;
using Cmf.Foundation.Common.Abstractions;
using Cmf.Navigo.BusinessObjects;
using Cmf.Navigo.BusinessObjects.Abstractions;
using Cmf.Navigo.BusinessOrchestration.Abstractions;
using Cmf.Navigo.BusinessOrchestration.LaborManagement.InputObjects;
using Cmf.Navigo.BusinessOrchestration.LaborManagement.OutputObjects;
using Cmf.Navigo.BusinessOrchestration.ResourceManagement.InputObjects;
using System.Data;

namespace Cmf.Custom.Common.SD.Utilities.Helpers;

/// <summary>
///     Employee Helpers
/// </summary>
/// <remarks>
///     Initializes a new instance of the <see cref="ResourceHelper"/> class.
/// </remarks>
/// <param name="entityFactory">
///     The entity factory.
/// </param>
/// <param name="laborOrchestration">
///     The labor orchestration.
/// </param>
/// <param name="areaHelpers">
///     The area helpers.
/// </param>
public class ResourceHelper(IEntityFactory entityFactory, ILaborOrchestration laborOrchestration, IAreaHelpers areaHelpers, IResourceOrchestration resourceOrchestration) : IResourceHelpers
{
    private readonly IEntityFactory _entityFactory = entityFactory;
    private readonly ILaborOrchestration _laborOrchestration = laborOrchestration;
    private readonly IAreaHelpers _areaHelpers = areaHelpers;
    private readonly IResourceOrchestration _resourceOrchestration = resourceOrchestration;

    /// <inheritdoc/>
    public IResourceCollection GetResourcesCheckedInByEmployee(IEmployee employee)
    {
        IResourceCollection finalResourceCollection = _entityFactory.CreateCollection<IResourceCollection>();

        if (employee == null) { return finalResourceCollection; }

        foreach (IArea area in _areaHelpers.GetAreas())
        {
            GetResourcesCheckedInByAreaOutput getResourcesOutput = _laborOrchestration.GetResourcesCheckedInByArea(new GetResourcesCheckedInByAreaInput()
            {
                Employee = employee,
                Area = area,
            });

            if (getResourcesOutput.Resources == null || getResourcesOutput.Resources.Count == 0)
            {
                continue;
            }

            finalResourceCollection.AddRange(getResourcesOutput.Resources);
        }

        return finalResourceCollection;
    }

    /// <inheritdoc/>
    public DataSet GetChecklistInstanceForResource(long resourceId, List<string> checklistTypes)
    {
        IQueryObject query = _entityFactory.Create<IQueryObject>();
        query.EntityTypeName = "ChecklistInstance";
        query.Name = "GetChecklistInstanceForResource";
        query.Query = new Query()
        {
            Distinct = true,
            Filters = new FilterCollection() {
                    new Filter()
                    {
                        Name = "UniversalState",
                        ObjectName = "ChecklistInstance",
                        ObjectAlias = "ChecklistInstance_1",
                        Operator = Cmf.Foundation.Common.FieldOperator.IsNotEqualTo,
                        Value = Cmf.Foundation.Common.Base.UniversalState.Terminated,
                        LogicalOperator = Cmf.Foundation.Common.LogicalOperator.AND,
                        FilterType = Cmf.Foundation.BusinessObjects.QueryObject.Enums.FilterType.Normal,
                    },
                    new Filter()
                    {
                        Name = "Resource",
                        ObjectName = "ChecklistInstance",
                        ObjectAlias = "ChecklistInstance_1",
                        Operator = Cmf.Foundation.Common.FieldOperator.IsEqualTo,
                        Value = resourceId,
                        LogicalOperator = Cmf.Foundation.Common.LogicalOperator.Nothing,
                        FilterType = Cmf.Foundation.BusinessObjects.QueryObject.Enums.FilterType.Normal,
                    },
                    new Filter()
                    {
                        Name = "Type",
                        ObjectName = "Checklist",
                        ObjectAlias = "ChecklistInstance_ParentEntity_2",
                        Operator = Cmf.Foundation.Common.FieldOperator.In,
                        Value = checklistTypes,
                        LogicalOperator = Cmf.Foundation.Common.LogicalOperator.Nothing,
                        FilterType = Cmf.Foundation.BusinessObjects.QueryObject.Enums.FilterType.Normal,
                    }
                },
            Fields = new FieldCollection() {
                    new Field()
                    {
                        Alias = "Name",
                        ObjectName = "ChecklistInstance",
                        ObjectAlias = "ChecklistInstance_1",
                        IsUserAttribute = false,
                        Name = "Name",
                        Position = 0,
                        Sort = Cmf.Foundation.Common.FieldSort.NoSort
                    }
                },
            Relations = new RelationCollection() {
                    new Relation()
                    {
                        Alias = "",
                        IsRelation = false,
                        Name = "",
                        SourceEntity = "ChecklistInstance",
                        SourceEntityAlias = "ChecklistInstance_1",
                        SourceJoinType = Cmf.Foundation.BusinessObjects.QueryObject.Enums.JoinType.InnerJoin,
                        SourceProperty = "ParentEntityId",
                        TargetEntity = "Checklist",
                        TargetEntityAlias = "ChecklistInstance_ParentEntity_2",
                        TargetJoinType = Cmf.Foundation.BusinessObjects.QueryObject.Enums.JoinType.InnerJoin,
                        TargetProperty = "Id"
                    }
                }
        };

        return query.Execute(false, null);
    }

    /// <inheritdoc/>
    public void AttachConsumablesToResource(IResource instance, IMaterialCollection materials, int order = 0)
    {
        Dictionary<IMaterial, IAttachConsumableParameters> materialToAttach = new();

        foreach (IMaterial material in materials)
        {
            IMaterialResource materialResource = _entityFactory.Create<IMaterialResource>();
            materialResource.SourceEntity = material;
            materialResource.TargetEntity = instance;

            AttachConsumableParameters consumableParameters = new()
            {
                Order = order,
                ResourceContainers = null,
                ResourceMaterial = materialResource
            };

            materialToAttach.Add(material, consumableParameters);
        }

        // Attach the materials
        if (materialToAttach.Count > 0)
        {
            AttachConsumablesToResourceInput input = new()
            {
                Resource = instance,
                Materials = materialToAttach
            };

            _resourceOrchestration.AttachConsumablesToResource(input);
        }
    }
}
