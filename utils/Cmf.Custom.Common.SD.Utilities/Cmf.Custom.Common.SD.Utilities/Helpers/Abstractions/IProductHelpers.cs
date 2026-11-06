using Cmf.Foundation.BusinessObjects.Abstractions;
using Cmf.Foundation.BusinessObjects.QueryObject;
using Cmf.Navigo.BusinessObjects.Abstractions;
using System.Collections.Generic;
using System.Data;

namespace Cmf.Custom.Common.SD.Utilities.Helpers.Abstractions;

/// <summary>
///     Product Helpers
/// </summary>
public interface IProductHelpers
{
    /// <summary>
    ///     Gets all characteristics of the specified <see cref="IProduct"/>.
    /// </summary>
    /// <param name="product">
    ///     The product.
    /// </param>
    /// <returns>
    ///     An <see cref="IProductCharacteristicCollection"/> containing the product characteristics.
    /// </returns>
    public IProductCharacteristicCollection GetProductCharacteristics(IProduct product);

    /// <summary>
    ///     Gets the BOM products that match the specified criteria.
    /// </summary>
    /// <param name="productTofind">
    ///     A dictionary that maps product names to the corresponding material names.
    /// </param>
    /// <param name="bomName">
    ///     The BOM name.
    /// </param>
    /// <param name="filterCollection">
    ///     The filters to apply to the query.
    /// </param>
    /// <returns>
    ///     A <see cref="DataSet"/> containing the matching BOM products.
    /// </returns>
    public DataSet GetBomProducts(Dictionary<string, string> productTofind, string bomName, FilterCollection filterCollection);
}