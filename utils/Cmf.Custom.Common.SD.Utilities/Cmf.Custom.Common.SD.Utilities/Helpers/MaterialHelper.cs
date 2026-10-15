using Cmf.Common.CustomActionUtilities.Extensions;
using Cmf.Custom.Common.SD.Utilities.Extensions;
using Cmf.Custom.Common.SD.Utilities.Helpers.Abstractions;
using Cmf.Foundation.BusinessObjects.Abstractions;
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
public class MaterialHelper(IEntityFactory entityFactory, IGenericServiceOrchestration genericServiceOrchestration) : IMaterialHelpers
{
    private readonly IEntityFactory _entityFactory = entityFactory;
    private readonly IGenericServiceOrchestration _genericServiceOrchestration = genericServiceOrchestration;

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
}