using Cmf.Common.CustomActionUtilities.Extensions;
using Cmf.Custom.Common.SD.Utilities.Helpers.Abstractions;
using Cmf.Foundation.BusinessObjects.Abstractions;
using Cmf.Foundation.BusinessObjects.QueryObject;
using Cmf.Foundation.Common.Abstractions;
using System.Collections.ObjectModel;
using System.Data;
using System.Reflection;

namespace Cmf.Custom.Common.SD.Utilities.Helpers;

public class QueryHelpers(IEntityFactory _entityFactory) : IQueryHelpers
{
    public Collection<T> QueryEntities<T>(FilterCollection filterCollection, int levelsToLoad = 0) where T : IEntityBase
    {
        // Create an instance of this entity
        T instance = _entityFactory.Create<T>();

        // Get the entity type
        IEntityType entityType = instance.EntityType;

        // And finally create a collection from the entity type
        dynamic collection = entityType.GetCollectionInstance();

        string objectName = entityType.Name;
        string objectAlias = entityType.Name + "_1";

        Stack<IFilterCollection> filtersToUpdate = new();
        filtersToUpdate.Push(filterCollection);

        while (filtersToUpdate.Count > 0)
        {
            foreach (IFilter filter in filtersToUpdate.Pop())
            {
                if (filter.InnerFilters != null)
                {
                    filtersToUpdate.Push(filter.InnerFilters);
                }
                else
                {
                    filter.ObjectName = objectName;
                    filter.ObjectAlias = objectAlias;
                }
            }
        }

        #region Create the Query Object

        IQueryObject query = _entityFactory.Create<IQueryObject>();
        query.Description = string.Empty;
        query.EntityTypeName = objectName;
        query.Name = string.Empty;
        query.Query = new Query
        {
            Distinct = false,
            Filters = filterCollection ?? [],
            Fields = new FieldCollection() 
            {
                new Field()
                {
                    Alias = "Id",
                    ObjectName = objectName,
                    ObjectAlias = objectAlias,
                    IsUserAttribute = false,
                    Name = "Id",
                    Position = 0,
                    Sort = Cmf.Foundation.Common.FieldSort.NoSort
                }
            },
            Relations = new RelationCollection()
        };

        #endregion

        DataSet result = query.Execute(false, null);

        PropertyInfo? property = instance.GetType().GetProperty("Id");

        if (result.HasData())
        {
            foreach (DataRow row in result.Tables[0].Rows)
            {
                long id = row.Field<long>("Id");

                T? entity = _entityFactory.Create<T>();

                property?.SetValue(entity, id);

                collection?.Add(entity);
            }

            collection?.Load(levelsToLoad: levelsToLoad);
        }

        return collection;
    }
}
