using Cmf.Navigo.BusinessObjects.Abstractions;

namespace Cmf.Custom.Common.SD.Utilities.Extensions;

public static class MaterialExtensions
{
    /// <summary>Returns the sum of a material's primary quantity and its submaterials' primary quantity.</summary>
    /// <param name="material">The material.</param>
    /// <returns>The total primary quantity.</returns>
    public static decimal TotalPrimaryQuantity(this IMaterial material)
    {
        return (material.PrimaryQuantity ?? 0) + (material.SubMaterialsPrimaryQuantity ?? 0);
    }
}
