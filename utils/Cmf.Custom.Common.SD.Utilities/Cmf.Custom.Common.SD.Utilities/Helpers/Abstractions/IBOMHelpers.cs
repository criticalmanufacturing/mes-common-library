using Cmf.Navigo.BusinessObjects.Abstractions;

namespace Cmf.Custom.Common.SD.Utilities.Helpers.Abstractions;

/// <summary>
///     BOM Helpers
/// </summary>
internal interface IBOMHelpers
{
    /// <summary>
    /// Gets the BOMs associated with BOM products whose product names match the specified names.
    /// </summary>
    /// <param name="productNames">The product names to search for.</param>
    /// <returns>A collection of BOMs associated with the matching products.</returns>
    public IBOMCollection GetBOMAssociatedWithProducts(IList<string> productNames);

    /// <summary>
    ///     Searches MES for a BOM which attribute hash matches the input.
    /// </summary>
    /// <param name="hash">
    ///     The value used in the query
    /// </param>
    /// <returns>
    ///     The BOM whose hash attribute matches with the one received
    /// </returns>
    public IBOM GetBOMByHash(string hash);
}

