using Cmf.Foundation.Configuration.Abstractions;
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
}