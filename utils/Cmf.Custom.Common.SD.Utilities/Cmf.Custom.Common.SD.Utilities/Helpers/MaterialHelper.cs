using Cmf.Custom.Common.SD.Utilities.Extensions;
using Cmf.Custom.Common.SD.Utilities.DataStructures;
using Cmf.Foundation.BusinessOrchestration;
using Cmf.Navigo.BusinessObjects;
using Cmf.Navigo.BusinessOrchestration.Abstractions;
using Cmf.Navigo.BusinessOrchestration.MaterialManagement.InputObjects;
using Cmf.Navigo.BusinessOrchestration.MaterialManagement.OutputObjects;
using System.Linq;
using Cmf.Custom.Common.SD.Utilities.Helpers.Abstractions;
using Cmf.Foundation.BusinessObjects.Abstractions;
using Cmf.Foundation.BusinessObjects;
using Cmf.Foundation.BusinessObjects.QueryObject;
using Cmf.Foundation.BusinessObjects.QueryObject.Enums;
using Cmf.Foundation.BusinessOrchestration.Abstractions;
using Cmf.Foundation.BusinessOrchestration.GenericServiceManagement.InputObjects;
using Cmf.Foundation.BusinessOrchestration.GenericServiceManagement.OutputObjects;
using Cmf.Foundation.Common;
using Cmf.Foundation.Common.Abstractions;
using Cmf.Foundation.Common.Base;
using Cmf.Navigo.BusinessObjects.Abstractions;
using System.Data;

namespace Cmf.Custom.Common.SD.Utilities.Helpers;

/// <summary>
///     Material Helpers
/// </summary>
/// <remarks>
///     Initializes a new instance of the <see cref="MaterialHelper"/> class.
/// </remarks>
/// <param name="entityFactory">
///     The entity factory.
/// </param>
/// <param name="genericServiceOrchestration">
///     The generic service orchestration.
/// </param>
public class MaterialHelper(IEntityFactory entityFactory, IGenericServiceOrchestration genericServiceOrchestration, IDEEHelper deeHelper, IMaterialOrchestration materialOrchestration) : IMaterialHelpers
{
    private readonly IEntityFactory _entityFactory = entityFactory;
    private readonly IGenericServiceOrchestration _genericServiceOrchestration = genericServiceOrchestration;
    private readonly IDEEHelper _deeHelper = deeHelper;
    private readonly IMaterialOrchestration _materialOrchestration = materialOrchestration;
    private readonly IFlowSearchHelper _flowSearchHelper = new FlowSearchHelper(entityFactory);

    /// <inheritdoc/>
    public DataSet GetChecklistInstanceForMaterial(long materialId)
    {
        IQueryObject query = _entityFactory.Create<IQueryObject>();
        query.EntityTypeName = "ChecklistInstance";
        query.Name = "GetChecklistInstanceForMaterial";
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
                        Name = "Material",
                        ObjectName = "ChecklistInstance",
                        ObjectAlias = "ChecklistInstance_1",
                        Operator = Cmf.Foundation.Common.FieldOperator.IsEqualTo,
                        Value = materialId,
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
                }
        };

        return query.Execute(false, null);
    }

    /// <inheritdoc/>
    public IMaterialCharacteristicCollection GetMaterialCharacteristics(IMaterial material)
    {
        IQueryObject query = _entityFactory.Create<IQueryObject>();

        query.Name = "GetMaterialCharacteristics";
        query.Description = "Get all Characteristics of a given Material";
        query.EntityTypeName = Navigo.Common.Constants.MaterialCharacteristic;
        query.Query = new Query
        {
            Distinct = false,
            Filters = new FilterCollection {
                    new Filter
                    {
                        Name = Foundation.Common.Constants.Id,
                        ObjectName = Navigo.Common.Constants.Material,
                        ObjectAlias = "MaterialCharacteristic_Material_2",
                        Operator = FieldOperator.IsEqualTo,
                        Value = material.Id,
                        LogicalOperator = LogicalOperator.AND,
                        FilterType = FilterType.Normal,
                    },
                    new Filter
                    {
                        Name = Foundation.Common.Constants.UniversalState,
                        ObjectName = Navigo.Common.Constants.MaterialCharacteristic,
                        ObjectAlias = "MaterialCharacteristic_1",
                        Operator = FieldOperator.IsNotEqualTo,
                        Value = UniversalState.Terminated,
                        LogicalOperator = LogicalOperator.Nothing,
                        FilterType = FilterType.Normal,
                    }
                },
            Fields = new FieldCollection {
                    new Field
                    {
                        Alias = Foundation.Common.Constants.Id,
                        ObjectName = Navigo.Common.Constants.MaterialCharacteristic,
                        ObjectAlias = "MaterialCharacteristic_1",
                        IsUserAttribute = false,
                        Name = Foundation.Common.Constants.Id,
                        Position = 0,
                        Sort = FieldSort.NoSort
                    },
                    new Field
                    {
                        Alias = Foundation.Common.Constants.Name,
                        ObjectName = Navigo.Common.Constants.MaterialCharacteristic,
                        ObjectAlias = "MaterialCharacteristic_1",
                        IsUserAttribute = false,
                        Name = Foundation.Common.Constants.Name,
                        Position = 1,
                        Sort = FieldSort.NoSort
                    }
                },
            Relations = new RelationCollection {
                    new Relation
                    {
                        Alias = string.Empty,
                        IsRelation = false,
                        Name = string.Empty,
                        SourceEntity = Navigo.Common.Constants.MaterialCharacteristic,
                        SourceEntityAlias = "MaterialCharacteristic_1",
                        SourceJoinType = JoinType.InnerJoin,
                        SourceProperty = "MaterialId",
                        TargetEntity = Navigo.Common.Constants.Material,
                        TargetEntityAlias = "MaterialCharacteristic_Material_2",
                        TargetJoinType = JoinType.InnerJoin,
                        TargetProperty = Foundation.Common.Constants.Id
                    }
                }

        };


        DataSet outputDataSet = query.Execute(false, new QueryParameterCollection());

        IMaterialCharacteristicCollection materialCharacteristics = _entityFactory.CreateCollection<IMaterialCharacteristicCollection>();

        if (outputDataSet.HasData())
        {
            GetObjectsByFilterInput getObjectsByFilterInput = new GetObjectsByFilterInput
            {
                Type = _entityFactory.Create<IMaterialCharacteristic>(),
                Filter = new FilterCollection()
            };

            foreach (DataRow outputRow in outputDataSet.Tables[0].Rows)
            {
                getObjectsByFilterInput.Filter.Add(
                    new Filter
                    {
                        Name = Foundation.Common.Constants.Id,
                        Operator = FieldOperator.IsEqualTo,
                        Value = outputRow[Foundation.Common.Constants.Id],
                        LogicalOperator = LogicalOperator.OR
                    }
                );
            }

            GetObjectsByFilterOutput getObjectsByFilterOutput = _genericServiceOrchestration.GetObjectsByFilter(getObjectsByFilterInput);

            if (getObjectsByFilterOutput?.Instance != null && getObjectsByFilterOutput?.Instance?.Count > 0)
            {
                materialCharacteristics.AddRange(getObjectsByFilterOutput.Instance.Cast<IMaterialCharacteristic>());
            }
        }

        return materialCharacteristics;
    }

    /// <inheritdoc/>
    public void MoveMaterialToStep(IMaterial material, string flowPath, BaseOutput serviceOutput = null, BaseInput serviceInput = null, string comment = null)
    {
        string serviceComment = serviceInput.GetAppendedServiceComment(comment);
        MoveMaterialToStepOutput result = _materialOrchestration.MoveMaterialToStep(new MoveMaterialToStepInput { FlowPath = flowPath, Material = material, ServiceComments = serviceComment });
        serviceOutput.MergeServiceOutputMessages(result);
    }

    /// <inheritdoc/>
    public IMaterial RenameMaterial(IMaterial material, string newName, BaseOutput serviceOutput = null, BaseInput serviceInput = null, string comment = null, string feedbackMessage = null)
    {
        string serviceComment = serviceInput.GetAppendedServiceComment(comment);
        IMaterialContainer newContainer = null;
        material.LoadRelations("MaterialContainer");
        if (material.MaterialContainer != null && material.MaterialContainer.Count > 0)
        {
            IMaterialContainer oldContainer = material.MaterialContainer.FirstOrDefault();
            if (oldContainer != null)
            {
                newContainer = _entityFactory.Create<IMaterialContainer>();
                newContainer.TargetEntity = oldContainer.TargetEntity;
                newContainer.Position = oldContainer.Position;
            }
            oldContainer.TargetEntity.DisassociateMaterials(material.MaterialContainer);
        }

        ISplitInputSubMaterialCollection splitSubMaterials = null;
        if (material.SubMaterialCount > 0)
        {
            splitSubMaterials = new SplitInputSubMaterialCollection();
            material.LoadChildren();
            foreach (IMaterial subMaterial in material.SubMaterials) splitSubMaterials.Add(new SplitInputSubMaterial { SubMaterial = subMaterial });
        }
        ISplitInputParameters splitParameters = new SplitInputParameters
        {
            Name = newName,
            PrimaryQuantity = material.PrimaryQuantity ?? 0,
            SecondaryQuantity = material.SecondaryQuantity,
            MaterialContainer = newContainer,
            SubMaterials = splitSubMaterials
        };
        SplitMaterialOutput splitOutput = _materialOrchestration.SplitMaterial(new SplitMaterialInput
        {
            Material = material,
            ChildMaterials = new SplitInputParametersCollection { splitParameters },
            TerminateOnZeroQuantity = true,
            ToCopyFutureGeneralActions = true,
            ServiceComments = serviceComment
        });
        IMaterial renamed = splitOutput.ChildMaterials?.FirstOrDefault();
        FeedbackMessage additionalMessage = string.IsNullOrWhiteSpace(feedbackMessage) ? null : new FeedbackMessage { Message = feedbackMessage, MessageType = FeedbackMessageType.Information };
        serviceOutput.MergeServiceOutputMessages(splitOutput, additionalMessage);
        return renamed;
    }

    /// <inheritdoc/>
    public IMaterialCollection GetLotsInContainer(IContainer container)
    {
        IMaterialCollection lots = null;
        container.LoadRelations("MaterialContainer");
        if (container.ContainerMaterials != null && container.ContainerMaterials.Count > 0)
        {
            IMaterialCollection materials = _entityFactory.CreateCollection<IMaterialCollection>();
            foreach (IMaterialContainer materialContainer in container.ContainerMaterials) materials.Add(materialContainer.SourceEntity);
            lots = GetLotsInMaterialStructure(materials);
        }
        return lots;
    }

    /// <inheritdoc/>
    public IMaterialCollection GetMaterialsInMaterialStructure(IMaterialCollection materials, string form)
    {
        IMaterialCollection result = null;
        if (materials != null && materials.Count > 0)
        {
            HashSet<long> processedIds = new();
            Dictionary<long, IMaterial> found = new();
            foreach (IMaterial topMost in materials.Select(material => material.GetTopMostMaterial()))
            {
                if (topMost == null || !processedIds.Add(topMost.Id)) continue;
                if ((string.IsNullOrEmpty(form) || topMost.Form == form) && !found.ContainsKey(topMost.Id)) found.Add(topMost.Id, topMost);
                INgpDataSet children = topMost.GetAllChildren(null);
                DataSet dataSet = children == null ? null : NgpDataSet.ToDataSet(children);
                if (dataSet == null) continue;
                using DataTableReader reader = dataSet.CreateDataReader();
                while (reader.Read())
                {
                    long childId = (long)reader["MaterialId"];
                    if (!processedIds.Add(childId)) continue;
                    IMaterial child = _entityFactory.Create<IMaterial>();
                    child.Load(childId);
                    if ((string.IsNullOrEmpty(form) || child.Form == form) && !found.ContainsKey(child.Id)) found.Add(child.Id, child);
                }
            }
            if (found.Count > 0)
            {
                result = _entityFactory.CreateCollection<IMaterialCollection>();
                foreach (IMaterial material in found.Values) result.Add(material);
            }
        }
        return result;
    }

    /// <inheritdoc/>
    public IMaterialCollection GetMaterialsInMaterialStructure(IMaterial material, string form)
    {
        IMaterialCollection materials = _entityFactory.CreateCollection<IMaterialCollection>();
        materials.Add(material);
        return GetMaterialsInMaterialStructure(materials, form);
    }

    /// <inheritdoc/>
    public IMaterialCollection GetLotsInMaterialStructure(IMaterialCollection materials) => GetMaterialsInMaterialStructure(materials, "Lot");

    /// <inheritdoc/>
    public IMaterialCollection GetLotsInMaterialStructure(IMaterial material)
    {
        IMaterialCollection materials = _entityFactory.CreateCollection<IMaterialCollection>();
        materials.Add(material);
        return GetLotsInMaterialStructure(materials);
    }

    /// <inheritdoc/>
    public AttachedConsumableCollection GetAttachedConsumables(IResource mainResource, short consumableFeedSearchDepth = 1)
    {
        AttachedConsumableCollection result = new();
        mainResource.GetDescendentResources(consumableFeedSearchDepth);
        if (mainResource.SubResources != null && mainResource.SubResources.Count > 0)
        {
            foreach (IResource feed in mainResource.SubResources.Where(resource => resource.ProcessingType == ProcessingType.ConsumableFeed))
            {
                IResourceMaterialInformationCollection materialInfo = feed.GetResourceMaterialInformation(true);
                if (materialInfo == null) continue;
                foreach (IResourceMaterialInformation info in materialInfo)
                {
                    result.Add(new AttachedConsumable { Consumable = info.Material, ConsumableFeed = feed, Order = info.Order });
                }
            }
        }
        return result;
    }

    /// <inheritdoc/>
    public IFlow GetStepParentFlow(IMaterial material)
    {
        string[] parts = material.FlowPath.Split('/');
        if (parts.Length == 2) return material.Flow;
        string flowName = material.FlowPath.Substring(0, material.FlowPath.LastIndexOf('/'));
        flowName = flowName.Substring(0, flowName.LastIndexOf(':'));
        flowName = flowName.Substring(flowName.LastIndexOf('/') + 1);
        IFlow flow = _entityFactory.Create<IFlow>();
        flow.Load(flowName);
        return flow;
    }

    /// <inheritdoc/>
    public IBOMProductCollection ResolveBOMProducts(IMaterial material)
    {
        IBOMProductCollection result = _entityFactory.CreateCollection<IBOMProductCollection>();
        _flowSearchHelper.GetFlowStepFromFlowPath(material.FlowPath, out IFlowCollection flows, out IStep step);
        IFilterCollection filters = new FilterCollection
        {
            new Filter { Name = Navigo.Common.Constants.Product, Operator = FieldOperator.IsEqualTo, Value = material.Product.Name },
            new Filter { Name = Navigo.Common.Constants.Flow, Operator = FieldOperator.IsEqualTo, Value = flows.Last().Name }
        };
        step.LoadBomContexts(filters);
        DataSet contextDataSet = NgpDataSet.ToDataSet(step.StepBomContexts);
        if (contextDataSet.HasData())
        {
            string bomName = contextDataSet.Tables[0].Rows[0][Navigo.Common.Constants.BOM].ToString();
            IBOM bom = _entityFactory.Create<IBOM>();
            bom.Load(bomName);
            bom.LoadRelations(Navigo.Common.Constants.BOMProduct);
            result = bom.BomProducts;
        }
        return result;
    }



}
