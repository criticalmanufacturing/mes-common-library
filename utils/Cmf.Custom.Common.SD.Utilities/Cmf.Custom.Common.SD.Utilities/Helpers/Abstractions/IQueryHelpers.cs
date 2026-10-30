using Cmf.Foundation.BusinessObjects.Abstractions;
using Cmf.Foundation.BusinessObjects.QueryObject;
using System.Collections.ObjectModel;

namespace Cmf.Custom.Common.SD.Utilities.Helpers.Abstractions;

public interface IQueryHelpers
{
    public Collection<T> QueryEntities<T>(FilterCollection filterCollection, int levelsToLoad = 0) where T : IEntityBase;
}
