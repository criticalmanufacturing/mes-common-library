using Cmf.Navigo.BusinessObjects.Abstractions;

namespace Cmf.Custom.Common.SD.Utilities.Helpers.Abstractions;

/// <summary>
///     Production Order Helpers
/// </summary>
public interface IProductionOrderHelpers
{
    /// <summary>
    ///     Gets all active characteristics of the specified <see cref="IProductionOrder"/>.
    /// </summary>
    /// <param name="productionOrder">
    ///     The production order.
    /// </param>
    /// <returns>
    ///     An <see cref="IProductionOrderCharacteristicCollection"/> containing the production order characteristics.
    /// </returns>
    public IProductionOrderCharacteristicCollection GetProductionOrderCharacteristics(IProductionOrder productionOrder);
}