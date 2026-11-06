using Cmf.Navigo.BusinessObjects.Abstractions;
using System.Collections.Generic;

namespace Cmf.Custom.Common.SD.Utilities.Helpers.Abstractions;

/// <summary>
///     Provides search operations for Flow and Step business entities.
/// </summary>
public interface IFlowSearchHelper
{


    /// <summary>
    ///     Searches for a FlowPath matching the top-level Flow and Step names, recursively searching intermediate flows.
    /// </summary>
    /// <param name="flowName">The name of the top-level Flow.</param>
    /// <param name="stepName">The name of the Step.</param>
    /// <param name="subFlowNames">Names of the intermediate flows. If omitted, the first matching step is used regardless of intermediate flows.</param>
    /// <returns>The FlowPath of the first match, or <see langword="null"/> if no match is found.</returns>
    string CalculateFlowPath(string flowName, string stepName, string[] subFlowNames = null);

    /// <summary>
    ///     Gets the flows and step represented by a FlowPath.
    /// </summary>
    /// <param name="flowPath">The FlowPath to parse.</param>
    /// <param name="flows">The resulting flow collection, which is never <see langword="null"/>.</param>
    /// <param name="step">The resulting step, which can be <see langword="null"/>.</param>
    /// <param name="levelsToLoad">The depth to load for the flows and step; a negative value leaves them unloaded.</param>
    void GetFlowStepFromFlowPath(string flowPath, out IFlowCollection flows, out IStep step, int levelsToLoad = 0);

}
