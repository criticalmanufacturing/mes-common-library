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
using Cmf.Navigo.BusinessObjects.Abstractions;
using System.Data;

namespace Cmf.Custom.Common.SD.Utilities.Helpers;

/// <summary>
///     Employee Helpers
/// </summary>
/// <remarks>
///     Initializes a new instance of the <see cref="ProductionOrderHelper"/> class.
/// </remarks>
/// <param name="entityFactory">
///     The entity factory.
/// </param>
/// <param name="genericServiceOrchestration">
///     The generic service orchestration.
/// </param>
public class ProductionOrderHelper(IEntityFactory entityFactory, IGenericServiceOrchestration genericServiceOrchestration) : IProductionOrderHelpers
{
    private readonly IEntityFactory _entityFactory = entityFactory;
    private readonly IGenericServiceOrchestration _genericServiceOrchestration = genericServiceOrchestration;

    /// <inheritdoc/>
    public IProductionOrderCharacteristicCollection GetProductionOrderCharacteristics(IProductionOrder productionOrder)
    {
        IQueryObject query = _entityFactory.Create<IQueryObject>();
        query.Name = "GetProductionOrderCharacteristics";
        query.Description = "GetProductionOrderCharacteristics";
        query.EntityTypeName = "ProductionOrderCharacteristic";
        query.Query = new Query
        {
            Distinct = false,
            Filters = new FilterCollection {
                    new Filter
                    {
                        Name = "UniversalState",
                        ObjectName = "ProductionOrderCharacteristic",
                        ObjectAlias = "ProductionOrderCharacteristic_1",
                        Operator = FieldOperator.IsNotEqualTo,
                        Value = Foundation.Common.Base.UniversalState.Terminated,
                        LogicalOperator = LogicalOperator.AND,
                        FilterType = FilterType.Normal,
                    },
                    new Filter
                    {
                        Name = "Id",
                        ObjectName = "ProductionOrder",
                        ObjectAlias = "ProductionOrderCharacteristic_ProductionOrder_2",
                        Operator = FieldOperator.IsEqualTo,
                        Value = productionOrder.Id,
                        LogicalOperator = LogicalOperator.Nothing,
                        FilterType = FilterType.Normal,
                    }
                },
            Fields = new FieldCollection {
                    new Field
                    {
                        Alias = "Id",
                        ObjectName = "ProductionOrderCharacteristic",
                        ObjectAlias = "ProductionOrderCharacteristic_1",
                        IsUserAttribute = false,
                        Name = "Id",
                        Position = 0,
                        Sort = FieldSort.NoSort
                    },
                    new Field
                    {
                        Alias = "Name",
                        ObjectName = "ProductionOrderCharacteristic",
                        ObjectAlias = "ProductionOrderCharacteristic_1",
                        IsUserAttribute = false,
                        Name = "Name",
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
                        SourceEntity = "ProductionOrderCharacteristic",
                        SourceEntityAlias = "ProductionOrderCharacteristic_1",
                        SourceJoinType = JoinType.InnerJoin,
                        SourceProperty = "ProductionOrderId",
                        TargetEntity = "ProductionOrder",
                        TargetEntityAlias = "ProductionOrderCharacteristic_ProductionOrder_2",
                        TargetJoinType = JoinType.InnerJoin,
                        TargetProperty = "Id"
                    }
                }
        };
        DataSet outputDataSet = query.Execute(false, new QueryParameterCollection());

        IProductionOrderCharacteristicCollection productionOrderCharacteristics = _entityFactory.CreateCollection<IProductionOrderCharacteristicCollection>();

        if (outputDataSet.HasData())
        {
            GetObjectsByFilterInput getObjectsByFilterInput = new GetObjectsByFilterInput
            {
                Type = _entityFactory.Create<IProductionOrderCharacteristic>(),
                Filter = new FilterCollection()
            };

            foreach (DataRow outputRow in outputDataSet.Tables[0].Rows)
            {
                getObjectsByFilterInput.Filter.Add(
                    new Filter
                    {
                        Name = "Id",
                        Operator = FieldOperator.IsEqualTo,
                        Value = outputRow["Id"],
                        LogicalOperator = LogicalOperator.OR
                    }
                );
            }

            GetObjectsByFilterOutput getObjectsByFilterOutput = _genericServiceOrchestration.GetObjectsByFilter(getObjectsByFilterInput);

            if (getObjectsByFilterOutput?.Instance != null && getObjectsByFilterOutput?.Instance?.Count > 0)
            {
                productionOrderCharacteristics.AddRange(getObjectsByFilterOutput.Instance.Cast<IProductionOrderCharacteristic>());
            }
        }

        return productionOrderCharacteristics;
    }
}