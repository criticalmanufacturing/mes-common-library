using Cmf.Custom.Common.SD.Utilities.Extensions;
using Cmf.Custom.Common.SD.Utilities.Helpers.Abstractions;
using Cmf.Navigo.BusinessObjects;
using System.Linq;
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
public class FlowHelpers(IEntityFactory entityFactory, IGenericHelper genericHelper) : IFlowHelpers
{
    private readonly IEntityFactory _entityFactory = entityFactory;
    private readonly IGenericHelper _genericHelper = genericHelper;



    private static string GetFirstFlowPathForFlow(IFlow flow, long? flowCorrelationId)
    {
        string result = null;
        flow.LoadFlowItems();
        var firstItem = flow.FlowItems.OrderBy(item => item.Position).FirstOrDefault();
        if (firstItem != null)
        {
            result = firstItem.Type == FlowItemType.Flow
                ? string.Format("{0}:{1}:{2}/{3}", flow.Name, flow.Revision, flowCorrelationId, GetFirstFlowPathForFlow(firstItem.Flow, firstItem.CorrelationId))
                : string.Format("{0}:{1}:{2}/{3}:{4}", flow.Name, flow.Revision, flowCorrelationId, firstItem.Step.Name, firstItem.CorrelationId);
        }
        return result;
    }



    private static string GetLastFlowPathForFlow(IFlow flow, long? flowCorrelationId)
    {
        string result = null;
        flow.LoadFlowItems();
        var lastItem = flow.FlowItems.OrderByDescending(item => item.Position).FirstOrDefault();
        if (lastItem != null)
        {
            result = lastItem.Type == FlowItemType.Flow
                ? string.Format("{0}:{1}:{2}/{3}", flow.Name, flow.Revision, flowCorrelationId, GetLastFlowPathForFlow(lastItem.Flow, lastItem.CorrelationId))
                : string.Format("{0}:{1}:{2}/{3}:{4}", flow.Name, flow.Revision, flowCorrelationId, lastItem.Step.Name, lastItem.CorrelationId);
        }
        return result;
    }





    private static IStep GetStepOfFlow(IFlow flow, bool first)
    {
        flow.LoadFlowItems();
        var item = first
            ? flow.FlowItems.OrderBy(flowItem => flowItem.Position).FirstOrDefault()
            : flow.FlowItems.OrderByDescending(flowItem => flowItem.Position).FirstOrDefault();
        if (item == null) return null;
        return item.Type == FlowItemType.Flow ? GetStepOfFlow(item.Flow, first) : item.Step;
    }

    /// <inheritdoc/>
    public IStepCollection GetAllStepsOfFlow(IFlow flow, bool loadFlow = true)
    {
        IStepCollection result = _entityFactory.CreateCollection<IStepCollection>();
        if (loadFlow) flow.LoadFullFlowStructure(true);
        foreach (var item in flow.FlowItems.Where(flowItem => flowItem.Type == FlowItemType.Step)) result.Add(item.Step);
        foreach (var item in flow.FlowItems.Where(flowItem => flowItem.Type == FlowItemType.Flow)) result.AddRange(GetAllStepsOfFlow(item.Flow, false));
        return result;
    }

    /// <inheritdoc/>
    public IFlowCollection GetAllFlowsWithStep(IFlow flow, string stepName, bool loadFlow = true)
    {
        IFlowCollection result = _entityFactory.CreateCollection<IFlowCollection>();
        if (loadFlow) flow.LoadFullFlowStructure(true);
        if (flow.FlowItems.Where(item => item.Type == FlowItemType.Step).Any(item => item.Step.Name.IgnoreCaseEquals(stepName))) result.Add(flow);
        foreach (var item in flow.FlowItems.Where(flowItem => flowItem.Type == FlowItemType.Flow)) result.AddRange(GetAllFlowsWithStep(item.Flow, stepName, false));
        return result;
    }



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
