using Cmf.Navigo.BusinessObjects.Abstractions;

namespace Cmf.Custom.Common.SD.Utilities.Helpers.Abstractions;

/// <summary>
///     Data Collection Helpers
/// </summary>
public interface IDataCollectionHelpers
{
    /// <summary>
    ///     Gets the latest <see cref="IDataCollectionInstance"/> for each specified material.
    /// </summary>
    /// <param name="materialIds">
    ///     The material identifiers.
    /// </param>
    /// <returns>
    ///     An <see cref="IDataCollectionInstanceCollection"/> containing the latest Data Collection Instance for each material.
    /// </returns>
    public IDataCollectionInstanceCollection GetDataCollectionInstancesForMaterials(List<long> materialIds);
}