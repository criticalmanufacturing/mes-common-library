using Cmf.Common.CustomActionUtilities.Extensions;
using Cmf.Custom.Common.SD.Utilities.Helpers.Abstractions;
using Cmf.Foundation.BusinessObjects.Abstractions;
using Cmf.Foundation.BusinessObjects.QueryObject;
using Cmf.Foundation.Common;
using Cmf.Foundation.Common.Abstractions;
using Cmf.Navigo.BusinessObjects.Abstractions;
using System.Data;

namespace Cmf.Custom.Common.SD.Utilities.Helpers;

/// <summary>
///     Employee Helpers
/// </summary>
/// <remarks>
///     Initializes a new instance of the <see cref="FlowHelpers"/> class.
/// </remarks>
/// <param name="entityFactory">
///     The entity factory.
/// </param>
public class FlowHelpers(IEntityFactory entityFactory) : IFlowHelpers
{
    private readonly IEntityFactory _entityFactory = entityFactory;

    /// <inheritdoc/>
    public IFlow GetFlowByHash(string hash, string hashAttributeName)
    {
        IFlow flow = _entityFactory.Create<IFlow>();

        IQueryObject query = _entityFactory.Create<IQueryObject>();

        query.Description = "Searches MES for a Flow which attribute Hash matches the input";
        query.EntityTypeName = "Flow";
        query.Name = "GetFlowByHash";
        query.Query = new Query
        {
            Distinct = false,
            Filters = new FilterCollection {
                    new Filter
                    {
                        Name = "UniversalState",
                        ObjectName = "Flow",
                        ObjectAlias = "Flow_1",
                        Operator = FieldOperator.IsEqualTo,
                        Value = Foundation.Common.Base.UniversalState.Effective,
                        LogicalOperator = LogicalOperator.AND,
                        FilterType = Foundation.BusinessObjects.QueryObject.Enums.FilterType.Normal,
                    },
                    new Filter
                    {
                        Name = hashAttributeName,
                        ObjectName = "Flow",
                        ObjectAlias = "Flow_1",
                        Operator = FieldOperator.IsEqualTo,
                        Value = hash,
                        LogicalOperator = LogicalOperator.Nothing,
                        FilterType = Foundation.BusinessObjects.QueryObject.Enums.FilterType.Normal,
                    }
                },
            Fields = new FieldCollection {
                    new Field
                    {
                        Alias = "Id",
                        ObjectName = "Flow",
                        ObjectAlias = "Flow_1",
                        IsUserAttribute = false,
                        Name = "Id",
                        Position = 0,
                        Sort = FieldSort.NoSort
                    },
                    new Field
                    {
                        Alias = "DefinitionId",
                        ObjectName = "Flow",
                        ObjectAlias = "Flow_1",
                        IsUserAttribute = false,
                        Name = "DefinitionId",
                        Position = 1,
                        Sort = FieldSort.NoSort
                    },
                    new Field
                    {
                        Alias = "Revision",
                        ObjectName = "Flow",
                        ObjectAlias = "Flow_1",
                        IsUserAttribute = false,
                        Name = "Revision",
                        Position = 2,
                        Sort = FieldSort.NoSort
                    },
                    new Field
                    {
                        Alias = "Name",
                        ObjectName = "Flow",
                        ObjectAlias = "Flow_1",
                        IsUserAttribute = false,
                        Name = "Name",
                        Position = 3,
                        Sort = FieldSort.NoSort
                    }
                },
            Relations = new RelationCollection()
        };

        DataSet result = query.Execute(false, new QueryParameterCollection());

        if (result.HasData())
        {
            flow.Name = result.Tables[0].Rows[0]["Name"] as string;
            flow.Load();
        }

        return flow;
    }
}