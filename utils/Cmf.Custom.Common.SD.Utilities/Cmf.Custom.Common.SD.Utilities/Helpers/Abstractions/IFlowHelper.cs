using Cmf.Foundation.BusinessObjects.Abstractions;
using Cmf.Navigo.BusinessObjects.Abstractions;

namespace Cmf.Custom.Common.SD.Utilities.Helpers.Abstractions;

/// <summary>
///     Flow Helpers
/// </summary>
public interface IFlowHelpers
{
    /// <summary>
    ///     Gets the effective <see cref="IFlow"/> whose hash attribute matches the specified value.
    /// </summary>
    /// <param name="hash">
    ///     The hash value.
    /// </param>
    /// <param name="hashAttributeName">
    ///     The name of the flow attribute that stores the hash.
    /// </param>
    /// <returns>
    ///     The matching <see cref="IFlow"/>.
    /// </returns>
    public IFlow GetFlowByHash(string hash, string hashAttributeName);




    /// <summary>
    ///     Gets all steps in a flow and its subflows recursively.
    /// </summary>
    /// <param name="flow">
    ///     The flow whose steps are retrieved.
    /// </param>
    /// <param name="loadFlow">
    ///     If <see langword="true"/>, loads the full flow structure; otherwise, the structure must already be loaded.
    /// </param>
    /// <returns>
    ///     A collection containing the steps in the flow and its subflows.
    /// </returns>
    public IStepCollection GetAllStepsOfFlow(IFlow flow, bool loadFlow = true);
    /// <summary>
    ///     Gets the main flow and subflows that contain a step with the specified name.
    /// </summary>
    /// <param name="flow">
    ///     The main flow to search.
    /// </param>
    /// <param name="stepName">
    ///     The name of the step to find.
    /// </param>
    /// <param name="loadFlow">
    ///     If <see langword="true"/>, loads the full flow structure; otherwise, the structure must already be loaded.
    /// </param>
    /// <returns>
    ///     A collection of flows that contain the specified step.
    /// </returns>
    public IFlowCollection GetAllFlowsWithStep(IFlow flow, string stepName, bool loadFlow = true);

}