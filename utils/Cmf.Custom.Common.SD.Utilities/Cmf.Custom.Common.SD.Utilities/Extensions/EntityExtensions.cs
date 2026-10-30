using Cmf.Common.CustomActionUtilities.Extensions;
using Cmf.Foundation.BusinessObjects;
using Cmf.Foundation.BusinessObjects.Abstractions;
using System.Collections.ObjectModel;
using System.Data;
using System.Diagnostics;
using Cmf.Custom.Common.SD.Utilities.Helpers;
using Cmf.Foundation.BusinessObjects.Abstractions;
using Cmf.Foundation.Common;
using Cmf.Foundation.Common.Base;
using Microsoft.Extensions.DependencyInjection;
using System.Collections.ObjectModel;
using System.Linq.Expressions;
using System.Reflection;

namespace Cmf.Custom.Common.SD.Utilities.Extensions;

public static class EntityExtensions
{
    private static readonly QueryHelpers _queryHelpers = ApplicationContext.CurrentServiceProvider.GetRequiredService<QueryHelpers>();

    #region Private Methods

    /// <summary>
    /// Utility method used to get the property info based on an anonymous function.
    /// For example, when called like this, returns the PropertyInfo relating to the Name property of the Material class
    /// 
    /// ```
    /// GetPropertyInfo<Material, string>(material => material.Name);
    /// ```
    /// </summary>
    /// <typeparam name="TSource"></typeparam>
    /// <typeparam name="TProperty"></typeparam>
    /// <param name="propertyLambda"></param>
    /// <returns></returns>
    /// <exception cref="ArgumentException"></exception>
    private static PropertyInfo GetPropertyInfo<TSource, TProperty>(Expression<Func<TSource, TProperty>> propertyLambda)
    {
        Type type = typeof(TSource);

        if (propertyLambda.Body is not MemberExpression member)
        {
            throw new ArgumentException(string.Format("Expression '{0}' refers to a method, not a property.",
                propertyLambda.ToString()));
        }

        if (member.Member is not PropertyInfo propInfo)
        {
            throw new ArgumentException(string.Format("Expression '{0}' refers to a field, not a property.",
                propertyLambda.ToString()));
        }

        if (propInfo.ReflectedType is null || (type != propInfo.ReflectedType && !type.IsSubclassOf(propInfo.ReflectedType)))
        {
            throw new ArgumentException(string.Format("Expression '{0}' refers to a property that is not from type {1}.",
                propertyLambda.ToString(), type));
        }

        return propInfo;
    }

    #endregion Private Methods



    /// <summary>
    /// Retrieves the hash value stored in the specified attribute of an entity, if available
    /// </summary>
    /// <returns>The hash value stored in the entity's attribute or a default value if the attribute is not found</returns>
    public static string GetHashFromEntity(this IEntity entity, string hashAttributeName)
    {
        entity.LoadAttributes(new Collection<string> { hashAttributeName });

        if (!hashAttributeName.IsNullOrEmpty() && !entity.Attributes.IsNullOrEmpty() && entity.Attributes.ContainsKey(hashAttributeName))
        {
            return (string)entity.Attributes[hashAttributeName];
        }

        return default;
    }

    #region Relations

    /// <summary>
    /// Return an enumeration of Relation instances from a non-versioned entity of type <typeparamref name="T"/>.
    /// </summary>
    /// <typeparam name="T"><see cref="IEntityRelation"/> type to search for relations in <paramref name="instance"/>.</typeparam>
    /// <param name="instance">Instance to get relations.</param>
    /// <param name="loadRelation">Whether to load the relationships before trying to getting them.</param>
    /// <param name="levelsToLoad">Level of dependencies of the entity to load.</param>
    /// <returns><see cref="IEnumerable{T}"/> if any otherwise an empty enumerable.</returns>
    public static IEnumerable<T> GetRelations<T>(this IEntityInstance instance, bool loadRelation = false, int levelsToLoad = 0) where T : IEntityRelation
    {
        string relationName = typeof(T).IsInterface && typeof(T).Name.StartsWith('I')
            ? typeof(T).Name[1..]
            : typeof(T).Name;

        if (loadRelation)
        {
            instance.LoadRelations(relationName, levelsToLoad);
        }

        if (instance.RelationCollection?.ContainsKey(relationName) is true)
        {
            return instance.RelationCollection[relationName].OfType<T>().ToList();
        }

        return [];
    }

    /// <summary>
    /// Return an enumeration of Relation instances from a non-versioned entity of type <typeparamref name="T"/>.
    /// </summary>
    /// <typeparam name="T"><see cref="IEntityRelation"/> type to search for relations in <paramref name="instanceCollection"/>.</typeparam>
    /// <param name="instanceCollection">Instance to get relations.</param>
    /// <param name="loadRelation">Whether to load the relationships before trying to getting them.</param>
    /// <param name="levelsToLoad">Level of dependencies of the entity to load.</param>
    /// <returns><see cref="IEnumerable{T}"/> if any otherwise an empty enumerable.</returns>
    public static IEnumerable<T> GetRelations<T>(this IEnumerable<IEntityInstance> instanceCollection, bool loadRelation = false, int levelsToLoad = 0) where T : IEntityRelation
    {
        string relationName = typeof(T).IsInterface && typeof(T).Name.StartsWith('I')
            ? typeof(T).Name[1..]
            : typeof(T).Name;

        if (loadRelation && instanceCollection.Any())
        {
            dynamic? collection = instanceCollection as IEntityCollection<IEntityInstance>;
            if (collection is null)
            {
                collection = instanceCollection.First().EntityType.GetCollectionInstance();
                foreach (dynamic instance in instanceCollection)
                {
                    collection.Add(instance);
                }
            }
            collection.LoadRelations(relationName, levelsToLoad);
        }

        return instanceCollection.SelectMany(instance =>
        {
            if (instance.RelationCollection?.ContainsKey(relationName) == true)
            {
                return instance.RelationCollection[relationName].OfType<T>().ToList();
            }
            return [];
        });
    }

    /// <summary>
    /// Return an enumeration of Relation instances from a non-versioned entity of type <typeparamref name="T"/>.
    /// </summary>
    /// <typeparam name="T"><see cref="IEntityRelation"/> type to search for relations in <paramref name="instance"/>.</typeparam>
    /// <param name="instance">Instance to get relations.</param>
    /// <param name="loadRelation">Whether to load the relationships before trying to getting them.</param>
    /// <param name="levelsToLoad">Level of dependencies of the entity to load.</param>
    /// <returns><see cref="IEnumerable{T}"/> if any otherwise an empty enumerable.</returns>
    public static IEnumerable<T> GetRelations<T>(this IEntityVersion instance, bool loadRelation = false, int levelsToLoad = 0) where T : IEntityRelation
    {
        string relationName = typeof(T).IsInterface && typeof(T).Name.StartsWith('I')
            ? typeof(T).Name[1..]
            : typeof(T).Name;

        if (loadRelation)
        {
            instance.LoadRelations(relationName, levelsToLoad);
        }

        if (instance.RelationCollection?.ContainsKey(relationName) == true)
        {
            return instance.RelationCollection[relationName].OfType<T>().ToList();
        }

        return [];
    }

    /// <summary>
    /// Return an enumeration of Relation instances from a non-versioned entity of type <typeparamref name="T"/>.
    /// </summary>
    /// <typeparam name="T"><see cref="IEntityRelation"/> type to search for relations in <paramref name="instanceCollection"/>.</typeparam>
    /// <param name="instanceCollection">Instance to get relations.</param>
    /// <param name="loadRelation">Whether to load the relationships before trying to getting them.</param>
    /// <param name="levelsToLoad">Level of dependencies of the entity to load.</param>
    /// <returns><see cref="IEnumerable{T}"/> if any otherwise an empty enumerable.</returns>
    public static IEnumerable<T> GetRelations<T>(this IEnumerable<IEntityVersion> instanceCollection, bool loadRelation = false, int levelsToLoad = 0) where T : IEntityRelation
    {
        string relationName = typeof(T).IsInterface && typeof(T).Name.StartsWith('I')
            ? typeof(T).Name[1..]
            : typeof(T).Name;

        if (loadRelation && instanceCollection.Any())
        {
            dynamic? collection = instanceCollection as IEntityCollection<IEntityInstance>;
            if (collection is null)
            {
                collection = instanceCollection.First().EntityType.GetCollectionInstance();
                foreach (dynamic instance in instanceCollection)
                {
                    collection.Add(instance);
                }
            }
            collection.LoadRelations(relationName, levelsToLoad);
        }

        return instanceCollection.SelectMany(instance =>
        {
            if (instance.RelationCollection?.ContainsKey(relationName) == true)
            {
                return instance.RelationCollection[relationName].OfType<T>().ToList();
            }
            return [];
        });
    }

    #endregion Relations

    #region References

    /// <summary>
    /// Query all entities of type T from the database, that reference the instance from in the given property. 
    /// For example, the following line will return all a list of all materials that are currently in the given step.
    /// 
    /// ```
    /// step.GetReferences<Material>(material => material.Step)
    /// ```
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="instance"></param>
    /// <param name="propertyLambda"></param>
    /// <returns></returns>
    public static Collection<T> GetReferences<T>(this IEntityBase instance, Expression<Func<T, object>> propertyLambda) where T : IEntityBase
    {
        return GetReferences([instance], propertyLambda);
    }

    /// <summary>
    /// Query all entities of type T from the database, that reference one of the instances from in the given property. 
    /// For example, the following line will return all a list of all materials that are currently in the given step.
    /// 
    /// ```
    /// stepCollection.GetReferences<Material>(material => material.Step)
    /// ```
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="instances"></param>
    /// <param name="propertyLambda"></param>
    /// <returns></returns>
    public static Collection<T> GetReferences<T>(this IEnumerable<IEntityBase> instances, Expression<Func<T, object>> propertyLambda) where T : IEntityBase
    {
        if (!instances.Any())
        {
            return [];
        }

        PropertyInfo propertyInfo = GetPropertyInfo(propertyLambda);

        Collection<T> referenceInstances = _queryHelpers.QueryEntities<T>(
            [
                new Foundation.BusinessObjects.QueryObject.Filter
                {
                    Name = propertyInfo.Name + "Id",
                    Operator = Foundation.Common.FieldOperator.In,
                    Value = instances.Select(i => i.Id).ToList(),
                },
                new Foundation.BusinessObjects.QueryObject.Filter
                {
                    Name = "UniversalState",
                    Operator = Foundation.Common.FieldOperator.IsNotEqualTo,
                    Value = UniversalState.Terminated,
                }
            ]);

        Dictionary<long, IEntityBase> referencesById = instances.ToDictionary(inst => inst.Id);

        foreach (T reference in referenceInstances)
        {
            long? id = reference.GetNativeValue<long?>(propertyInfo.Name + "Id");

            if (id is not null && referencesById.TryGetValue(id.Value, out IEntityBase? cacheValue))
            {
                propertyInfo.SetValue(reference, cacheValue);
            }
        }

        return referenceInstances;
    }

    #endregion References
}