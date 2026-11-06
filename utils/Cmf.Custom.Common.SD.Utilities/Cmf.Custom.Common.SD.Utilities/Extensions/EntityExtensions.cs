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

    public static void RemoveNullAttributes(this IEntity obj)
    {
        RemoveNullAttributes(obj.Attributes);
    }

    public static void RemoveNullAttributes(this IEntityVersion obj)
    {
        RemoveNullAttributes(obj.Attributes);
        RemoveNullAttributes(obj.RelatedAttributes);
    }

    private static void RemoveNullAttributes(IAttributeCollection? attributeCollection)
    {
        if (attributeCollection is null)
        {
            return;
        }

        Collection<string> keysToRemove = [];
        foreach (KeyValuePair<string, object> attribute in attributeCollection)
        {
            if (attribute.Value is null || attribute.Value is string value && string.IsNullOrEmpty(value))
            {
                keysToRemove.Add(attribute.Key);
            }
        }

        foreach (string key in keysToRemove)
        {
            attributeCollection.Remove(key);
        }
    }

    /// <summary>Returns a typed attribute value from an entity instance.</summary>
    /// <typeparam name="T">The attribute value type.</typeparam>
    /// <param name="entity">The entity containing the attribute.</param>
    /// <param name="attributeName">The attribute name.</param>
    /// <param name="loadAttribute">Whether to load the attribute before reading it.</param>
    /// <returns>The value, or the default value for <typeparamref name="T"/> if missing or of another type.</returns>
    public static T GetAttribute<T>(this IEntityInstance entity, string attributeName, bool loadAttribute = false)
    {
        T attributeValue = default(T);
        if (entity != null)
        {
            if (loadAttribute)
            {
                entity.LoadAttributes(new Collection<string> { attributeName });
            }
            if (entity.Attributes != null && entity.Attributes.ContainsKey(attributeName)
                && entity.Attributes[attributeName] != null && entity.Attributes[attributeName] is T)
            {
                attributeValue = (T)entity.Attributes[attributeName];
            }
        }
        return attributeValue;
    }

    /// <summary>Returns a new instance of a reloaded entity.</summary>
    /// <typeparam name="T">The entity type.</typeparam>
    /// <param name="entity">The entity to reload.</param>
    /// <returns>A new entity instance loaded from the entity identifier.</returns>
    public static T ReloadEntity<T>(this T entity) where T : IEntity
    {
        T reloadedEntity = (T)entity.EntityType.GetInstance();
        reloadedEntity.Load(entity.Id);
        return reloadedEntity;
    }

    /// <summary>Validates that mandatory attributes on an entity type have values.</summary>
    /// <param name="entityType">The entity type metadata.</param>
    /// <param name="attributeValues">The attribute values to validate.</param>
    public static void ValidateMandatoryAttributes(this IEntityType entityType, IAttributeCollection attributeValues)
    {
        List<string> missingProperties = new List<string>();
        entityType.LoadProperties();
        foreach (IEntityTypeProperty prop in entityType.Properties)
        {
            if (prop.PropertyType == EntityTypePropertyType.Attribute && (prop.Description ?? "").ToLower().Contains("mandatory"))
            {
                if (attributeValues == null
                    || !attributeValues.ContainsKey(prop.Name)
                    || string.IsNullOrWhiteSpace(attributeValues[prop.Name].ToString()))
                {
                    missingProperties.Add(prop.Name);
                }
            }
        }
        if (missingProperties.Count > 0)
        {
            throw new MissingMandatoryPropertyCmfException(missingProperties.ToArray(), entityType.Name, true);
        }
    }

    /// <summary>Loads a change set and optionally requests its approval.</summary>
    /// <param name="changeSet">The change set to load.</param>
    /// <param name="requestApproval">Whether to request approval after loading.</param>
    public static void RequestApproval(this IChangeSet changeSet, bool requestApproval = false)
    {
        if (changeSet != null && changeSet.ObjectExists())
        {
            changeSet.Load();
            if (requestApproval)
            {
                changeSet.RequestApproval();
            }
        }
    }

    /// <summary>Checks whether an entity has relations with the specified name.</summary>
    /// <param name="obj">The entity to inspect.</param>
    /// <param name="relationName">The relation name.</param>
    /// <param name="loadRelation">Whether to load the relation before checking.</param>
    /// <returns>True when the relation collection contains at least one relation.</returns>
    public static bool HasRelations(this IEntity obj, string relationName, bool loadRelation = false)
    {
        bool hasRelations = false;
        if (obj != null && !string.IsNullOrEmpty(relationName))
        {
            if (loadRelation)
            {
                if (obj is IEntityVersion version)
                {
                    version.LoadRelations(relationName);
                }
                else if (obj is IEntityInstance instance)
                {
                    instance.LoadRelations(relationName);
                }
            }
            if (obj.RelationCollection != null && obj.RelationCollection.ContainsKey(relationName)
                && obj.RelationCollection[relationName] != null && obj.RelationCollection[relationName].Count > 0)
            {
                hasRelations = true;
            }
        }
        return hasRelations;
    }

    /// <summary>Checks whether an entity version has related relations with the specified name.</summary>
    /// <param name="obj">The entity version to inspect.</param>
    /// <param name="relationName">The related relation name.</param>
    /// <param name="loadRelation">Whether to load the relation before checking.</param>
    /// <returns>True when the related relation collection contains at least one relation.</returns>
    public static bool HasRelatedRelations(this IEntityVersion obj, string relationName, bool loadRelation = false)
    {
        bool hasRelations = false;
        if (obj != null && !string.IsNullOrEmpty(relationName))
        {
            if (loadRelation)
            {
                obj.LoadRelations(relationName);
            }
            if (obj.RelatedRelationCollection != null && obj.RelatedRelationCollection.ContainsKey(relationName)
                && obj.RelatedRelationCollection[relationName] != null && obj.RelatedRelationCollection[relationName].Count > 0)
            {
                hasRelations = true;
            }
        }
        return hasRelations;
    }

}