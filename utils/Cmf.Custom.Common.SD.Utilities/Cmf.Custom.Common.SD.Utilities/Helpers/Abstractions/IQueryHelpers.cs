using Cmf.Foundation.BusinessObjects.Abstractions;
using Cmf.Foundation.BusinessObjects.QueryObject;
using System.Collections.ObjectModel;

namespace Cmf.Custom.Common.SD.Utilities.Helpers.Abstractions;

/// <summary>
/// Provides helpers for querying entities.
/// </summary>
public interface IQueryHelpers
{
    /// <summary>
    /// Queries entities of the specified type that match the filters.
    /// </summary>
    /// <typeparam name="T">The entity type to query.</typeparam>
    /// <param name="filterCollection">The filters to apply to the query.</param>
    /// <param name="levelsToLoad">The relation depth to load for each matching entity.</param>
    /// <returns>A collection of matching entities.</returns>
    public Collection<T> QueryEntities<T>(FilterCollection filterCollection, int levelsToLoad = 0) where T : IEntityBase;
}
