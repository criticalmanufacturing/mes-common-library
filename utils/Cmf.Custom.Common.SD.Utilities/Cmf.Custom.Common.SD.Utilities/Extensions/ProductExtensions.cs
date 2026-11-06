using Cmf.Foundation.BusinessObjects;
using Cmf.Navigo.BusinessObjects.Abstractions;
using System;
using System.Data;

namespace Cmf.Custom.Common.SD.Utilities.Extensions;

public static class ProductExtensions
{
    /// <summary>Gets the unit conversion factor between two units for a product.</summary>
    /// <param name="product">The product.</param>
    /// <param name="fromUnit">The source unit.</param>
    /// <param name="toUnit">The target unit.</param>
    /// <param name="loadUnitConversionFactors">Whether to load conversion factors first.</param>
    /// <returns>The conversion factor, its reciprocal if only the reverse conversion exists, or null.</returns>
    public static decimal? GetProductConversionFactors(this IProduct product, string fromUnit, string toUnit, bool loadUnitConversionFactors = false)
    {
        if (product == null) throw new ArgumentNullException("Product");
        if (string.IsNullOrEmpty(fromUnit)) throw new ArgumentNullException("FromUnit");
        if (string.IsNullOrEmpty(toUnit)) throw new ArgumentNullException("ToUnit");
        if (loadUnitConversionFactors) product.LoadUnitConversionFactors();
        DataSet dataSet = NgpDataSet.ToDataSet(product.UnitConversionFactors.Data);
        string expression = string.Format("FromUnit = '{0}' and ToUnit = '{1}'", fromUnit, toUnit);
        DataRow[] rows = dataSet.Tables[0].Select(expression);
        if (rows.Length == 0)
        {
            expression = string.Format("FromUnit = '{0}' and ToUnit = '{1}'", toUnit, fromUnit);
            rows = dataSet.Tables[0].Select(expression);
            return rows.Length == 0 ? null : 1 / (decimal)(double)rows[0]["ConversionFactor"];
        }
        return (decimal)(double)rows[0]["ConversionFactor"];
    }
}
