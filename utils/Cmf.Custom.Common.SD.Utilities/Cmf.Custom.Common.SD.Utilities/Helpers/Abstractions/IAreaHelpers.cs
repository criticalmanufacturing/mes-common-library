using Cmf.Navigo.BusinessObjects.Abstractions;

namespace Cmf.Custom.Common.SD.Utilities.Helpers.Abstractions;

/// <summary>
///     Area Helpers
/// </summary>
public interface IAreaHelpers
{
    /// <summary>
    ///     Gets all <see cref="IArea"/> from database.
    /// </summary>
    /// <returns>
    ///     <see cref="IAreaCollection"/> containing a all Areas.
    /// </returns>
    public IAreaCollection GetAreas();
}

