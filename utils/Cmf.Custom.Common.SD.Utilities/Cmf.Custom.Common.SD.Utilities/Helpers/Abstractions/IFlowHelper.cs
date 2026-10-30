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
}