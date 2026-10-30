using Cmf.Navigo.BusinessObjects.Abstractions;

namespace Cmf.Custom.Common.SD.Utilities.Helpers.Abstractions;

/// <summary>
///     BOM Helpers
/// </summary>
internal interface IBOMHelpers
{
    /// <summary>
    /// Searched the production order for a specific component
    /// </summary>
    /// <param name="productionOrder">Production order to search</param>
    /// <param name="LotNumber">Lot to search on production order</param>
    /// <param name="OriginalQuantity">Original quantity of Lot</param>
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

