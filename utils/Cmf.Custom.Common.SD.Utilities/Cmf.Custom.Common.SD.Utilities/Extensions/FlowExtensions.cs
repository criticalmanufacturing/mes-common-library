using Cmf.Foundation.Configuration.Abstractions;
using Cmf.Foundation.Common;
using Cmf.Foundation.Common.Abstractions;
using Cmf.Navigo.BusinessObjects;
using System.Collections.Generic;
using System.Linq;
using Cmf.Navigo.BusinessObjects.Abstractions;

namespace Cmf.Custom.Common.SD.Utilities.Extensions;

public static class FlowExtensions
{
    /// <summary>
    ///     Gets the first step of a Flow.
    /// </summary>
    /// <param name="flow">
    ///     The flow.
    /// </param>
    /// <returns>
    ///     An IStep.
    /// </returns>
    public static IStep GetFirstStep(this IFlow flow)
    {
        flow.LoadFlowItems();

        IFlowItem firstFlowItem = flow.FlowItems.FirstOrDefault();

        // Check if 1st FlowItem is a Step
        if (firstFlowItem.Step != null)
        {
            // If yes, return it
            return firstFlowItem.Step;
        }
        else
        {
            // If no, check 1st Sub-Flow
            return GetFirstStep(firstFlowItem.Flow);
        }
    }

    /// <summary>
    ///     Gets the last step of a Flow.
    /// </summary>
    /// <param name="flow">
    ///     The flow.
    /// </param>
    /// <returns>
    ///     An IStep.
    /// </returns>
    public static IStep GetLastStep(this IFlow flow)
    {
        flow.LoadFlowItems();

        IFlowItem lastFlowItem = flow.FlowItems.LastOrDefault();

        // Check if last FlowItem is a Step
        if (lastFlowItem.Step != null)
        {
            // If yes, return it
            return lastFlowItem.Step;
        }
        else
        {
            // If no, check Sub-Flow
            return GetLastStep(lastFlowItem.Flow);
        }
    }

    /// <summary>Gets the path of the first step in a flow and its nested flows.</summary>
    /// <param name="flow">The flow.</param>
    /// <returns>The first flow path, or null when the flow has no items.</returns>
    public static string GetFirstFlowPathForFlow(this IFlow flow) => GetFirstFlowPath(flow, 1);

    private static string GetFirstFlowPath(IFlow flow, long? flowCorrelationId)
    {
        string result = null;
        flow.LoadFlowItems();
        var firstItem = flow.FlowItems.OrderBy(item => item.Position).FirstOrDefault();
        if (firstItem != null)
        {
            result = firstItem.Type == FlowItemType.Flow
                ? string.Format("{0}:{1}:{2}/{3}", flow.Name, flow.Revision, flowCorrelationId, GetFirstFlowPath(firstItem.Flow, firstItem.CorrelationId))
                : string.Format("{0}:{1}:{2}/{3}:{4}", flow.Name, flow.Revision, flowCorrelationId, firstItem.Step.Name, firstItem.CorrelationId);
        }
        return result;
    }

    /// <summary>Gets the path of the last step in a flow and its nested flows.</summary>
    /// <param name="flow">The flow.</param>
    /// <returns>The last flow path, or null when the flow has no items.</returns>
    public static string GetLastFlowPathForFlow(this IFlow flow) => GetLastFlowPath(flow, 1);

    private static string GetLastFlowPath(IFlow flow, long? flowCorrelationId)
    {
        string result = null;
        flow.LoadFlowItems();
        var lastItem = flow.FlowItems.OrderByDescending(item => item.Position).FirstOrDefault();
        if (lastItem != null)
        {
            result = lastItem.Type == FlowItemType.Flow
                ? string.Format("{0}:{1}:{2}/{3}", flow.Name, flow.Revision, flowCorrelationId, GetLastFlowPath(lastItem.Flow, lastItem.CorrelationId))
                : string.Format("{0}:{1}:{2}/{3}:{4}", flow.Name, flow.Revision, flowCorrelationId, lastItem.Step.Name, lastItem.CorrelationId);
        }
        return result;
    }

    /// <summary>Gets the first step in a flow, including nested flows.</summary>
    /// <param name="flow">The flow.</param>
    /// <returns>The first step, or null when the flow has no items.</returns>
    public static IStep GetFirstStepOfFlow(this IFlow flow) => GetStepOfFlow(flow, first: true);

    /// <summary>Gets the last step in a flow, including nested flows.</summary>
    /// <param name="flow">The flow.</param>
    /// <returns>The last step, or null when the flow has no items.</returns>
    public static IStep GetLastStepOfFlow(this IFlow flow) => GetStepOfFlow(flow, first: false);

    private static IStep GetStepOfFlow(IFlow flow, bool first)
    {
        flow.LoadFlowItems();
        var item = first
            ? flow.FlowItems.OrderBy(flowItem => flowItem.Position).FirstOrDefault()
            : flow.FlowItems.OrderByDescending(flowItem => flowItem.Position).FirstOrDefault();
        if (item == null) return null;
        return item.Type == FlowItemType.Flow ? GetStepOfFlow(item.Flow, first) : item.Step;
    }

    /// <summary>Gets flow steps and their positions starting from the supplied initial point.</summary>
    /// <param name="flow">The flow to traverse.</param>
    /// <param name="_level">The current nesting level.</param>
    /// <param name="_recurrenceFlowPath">The current flow path.</param>
    /// <param name="_positionOfStep">The position to assign to the next step.</param>
    /// <param name="initialStep">The starting step, if any.</param>
    /// <param name="initialFlowPath">The starting flow path, if any.</param>
    /// <param name="foundInitialStep">Whether traversal has reached the starting point.</param>
    /// <returns>Flow items mapped to their positions.</returns>
    public static Dictionary<IFlowItem, int> GetFlowStepsFromFlow(this IFlow flow, int _level = 0, string _recurrenceFlowPath = null, int _positionOfStep = 0, IStep initialStep = null, string initialFlowPath = null, bool foundInitialStep = false)
    {
        if (flow == null) throw new ArgumentNullCmfException("Flow");
        if (initialStep == null && string.IsNullOrEmpty(initialFlowPath)) foundInitialStep = true;
        flow.Load();
        flow.LoadFlowItems();
        if (string.IsNullOrEmpty(_recurrenceFlowPath)) _recurrenceFlowPath = string.Format("{0}:1", flow.Name);
        string[] recurrenceParts = _recurrenceFlowPath.Split(new[] { Cmf.Navigo.Common.Constants.FlowPathSpliter }, StringSplitOptions.RemoveEmptyEntries);
        string currentFlowPath = recurrenceParts[_level];
        long? fromCorrelationId = null;
        string[] correlationParts = currentFlowPath.Split(new[] { Cmf.Navigo.Common.Constants.FlowPathCorrelationIdSpliter }, StringSplitOptions.RemoveEmptyEntries);
        if (correlationParts.Length > 1 && long.TryParse(correlationParts[1], out long correlationId)) fromCorrelationId = correlationId;
        Dictionary<IFlowItem, int> steps = new();
        foreach (var item in flow.FlowItems)
        {
            string itemFlowPath = string.Format("{0}/{1}:{2}", _recurrenceFlowPath, item.Type == FlowItemType.Flow ? item.Flow.Name : item.Step.Name, item.CorrelationId);
            if (item.Type == FlowItemType.Flow)
            {
                int countBefore = steps.Count;
                int countAfter = steps.Count;
                int count = steps.Count;
                steps.AddRange(item.Flow.GetFlowStepsFromFlow(_level + 1, itemFlowPath, _positionOfStep + countAfter - countBefore, initialStep, initialFlowPath, foundInitialStep));
                if (!(item.Flow.IsAlternate ?? false)) countAfter = steps.Count;
                if (!foundInitialStep && count < steps.Count) foundInitialStep = true;
            }
            else if (item.Type == FlowItemType.Step)
            {
                IFlowItem existing = steps.Keys.FirstOrDefault(candidate => candidate.Id == item.Id);
                if (!foundInitialStep && initialStep != null && item.Step != null && item.Step.Id == initialStep.Id) foundInitialStep = true;
                if (!foundInitialStep && !string.IsNullOrWhiteSpace(initialFlowPath) && GenericExtensions.IgnoreCaseEquals(itemFlowPath, initialFlowPath)) foundInitialStep = true;
                if (foundInitialStep)
                {
                    if (existing != null && steps.ContainsKey(existing) && steps[existing] > _positionOfStep) steps[existing] = _positionOfStep;
                    else steps[item] = _positionOfStep;
                    _positionOfStep++;
                }
            }
        }
        return steps;
    }

    /// <summary>Calculates a flow path to a step, optionally through specified subflows.</summary>
    /// <param name="flow">The flow to search.</param>
    /// <param name="step">The target step.</param>
    /// <param name="middleFlows">Optional subflows along the path.</param>
    /// <param name="correlationId">The initial correlation identifier.</param>
    /// <returns>The matching flow path, or null when the step is not in the flow.</returns>
    public static string CalculateFlowPath(this IFlow flow, IStep step, List<IFlow> middleFlows = null, long? correlationId = 1)
    {
        if (flow == null || flow.Id <= 0)
        {
            throw new ArgumentNullCmfException("Flow");
        }
        if (step == null || step.Id <= 0)
        {
            throw new ArgumentNullCmfException("Step");
        }
        middleFlows ??= new List<IFlow>();
        string flowPath = null;
        string childFlowPath = null;
        flow.LoadFlowItems(0);
        foreach (var item in flow.FlowItems)
        {
            if (item.Type == FlowItemType.Flow)
            {
                if (middleFlows.Count > 0)
                {
                    IFlow nextSubFlow = middleFlows.First();
                    if (item.Flow.Id == nextSubFlow.Id)
                    {
                        List<IFlow> subMiddleFlows = new List<IFlow>(middleFlows);
                        subMiddleFlows.Remove(nextSubFlow);
                        childFlowPath = item.Flow.CalculateFlowPath(step, subMiddleFlows, item.CorrelationId ?? 0);
                    }
                }
                else
                {
                    childFlowPath = item.Flow.CalculateFlowPath(step, middleFlows, item.CorrelationId ?? 0);
                }
                if (!string.IsNullOrEmpty(childFlowPath))
                {
                    flowPath = flow.Name + Cmf.Navigo.Common.Constants.FlowPathCorrelationIdSpliter + flow.Revision
                        + Cmf.Navigo.Common.Constants.FlowPathCorrelationIdSpliter + correlationId;
                    flowPath += Cmf.Navigo.Common.Constants.FlowPathSpliter + childFlowPath;
                    break;
                }
            }
            else if (item.Type == FlowItemType.Step && item.Step.Id == step.Id)
            {
                flowPath = flow.Name + Cmf.Navigo.Common.Constants.FlowPathCorrelationIdSpliter + flow.Revision
                    + Cmf.Navigo.Common.Constants.FlowPathCorrelationIdSpliter + correlationId;
                flowPath += Cmf.Navigo.Common.Constants.FlowPathSpliter;
                flowPath += step.Name + Cmf.Navigo.Common.Constants.FlowPathCorrelationIdSpliter + item.CorrelationId;
                break;
            }
        }
        return flowPath;
    }

    /// <summary>Checks whether a flow path matches a sequence of flow names.</summary>
    /// <param name="flowPath">The path to check.</param>
    /// <param name="flowNames">The expected flow names.</param>
    /// <returns>True when the path matches the expected sequence.</returns>
    public static bool IsFlowPathMatch(this string flowPath, string[] flowNames)
    {
        bool isFlowPathMatched = false;
        System.Text.StringBuilder flowPathToCompare = new();
        if (flowNames != null && flowNames.Length > 0)
        {
            foreach (string flow in flowNames)
            {
                flowPathToCompare.Append(flow).Append(Cmf.Navigo.Common.Constants.FlowPathCorrelationIdSpliter);
                flowPathToCompare.Append("[0-9]+").Append(Cmf.Navigo.Common.Constants.FlowPathSpliter);
            }
            flowPathToCompare.Remove(flowPathToCompare.Length - 1, 1);
            isFlowPathMatched = System.Text.RegularExpressions.Regex.IsMatch(flowPath, flowPathToCompare.ToString());
        }
        return isFlowPathMatched;
    }

}