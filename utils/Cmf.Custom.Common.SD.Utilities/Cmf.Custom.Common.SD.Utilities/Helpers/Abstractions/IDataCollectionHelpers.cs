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

    /// <summary>
    ///     Loads a Data Collection Instance and its Data Collection Points.
    /// </summary>
    /// <param name="dcInstanceId">
    ///     The identifier of the Data Collection Instance. This identifier is assumed to exist.
    /// </param>
    /// <returns>
    ///     The Data Collection Instance loaded with its Data Collection Points.
    /// </returns>
    public IDataCollectionInstance LoadDCInstance(long dcInstanceId);


}