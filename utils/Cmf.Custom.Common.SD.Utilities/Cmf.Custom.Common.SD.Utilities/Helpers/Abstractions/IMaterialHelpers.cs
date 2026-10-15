using Cmf.Foundation.BusinessObjects.Abstractions;
using Cmf.Navigo.BusinessObjects.Abstractions;
using System.Data;

namespace Cmf.Custom.Common.SD.Utilities.Helpers.Abstractions;

/// <summary>
///     Material Helpers
/// </summary>
public interface IMaterialHelpers
{
    /// <summary>
    ///     Gets the checklist instances associated with the specified material.
    /// </summary>
    /// <param name="materialId">
    ///     The material identifier.
    /// </param>
    /// <returns>
    ///     A <see cref="DataSet"/> containing the checklist instances.
    /// </returns>
    public DataSet GetChecklistInstanceForMaterial(long materialId);

    /// <summary>
    ///     Gets all active characteristics of the specified <see cref="IMaterial"/>.
    /// </summary>
    /// <param name="material">
    ///     The material.
    /// </param>
    /// <returns>
    ///     An <see cref="IMaterialCharacteristicCollection"/> containing the material characteristics.
    /// </returns>
    public IMaterialCharacteristicCollection GetMaterialCharacteristics(IMaterial material);
}