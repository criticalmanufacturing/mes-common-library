using Cmf.Foundation.BusinessObjects.Abstractions;
using System.Collections.ObjectModel;
using System.Linq;

namespace Cmf.Custom.Common.SD.Utilities.Extensions;

public static class AttributeExtensions
{
    /// <summary>Returns the value of an entity relation attribute, loading it when requested.</summary>
    /// <typeparam name="T">The attribute value type.</typeparam>
    /// <param name="entity">The entity relation.</param>
    /// <param name="attributeName">The attribute name.</param>
    /// <param name="loadAttribute">Whether to load the attribute.</param>
    /// <returns>The attribute value, or the default value for <typeparamref name="T"/>.</returns>
    public static T GetAttributeValueOrDefault<T>(this IEntityRelation entity, string attributeName, bool loadAttribute = false)
    {
        T attributeValue = default;

        if (loadAttribute && (entity.Attributes == null || !entity.Attributes.ContainsKey(attributeName)))
        {
            entity.LoadAttributes(new Collection<string>() { attributeName });
        }

        if (entity.Attributes != null && entity.Attributes.ContainsKey(attributeName))
        {
            attributeValue = (T)entity.Attributes[attributeName];
        }

        return attributeValue;
    }

    /// <summary>Loads one attribute on an entity instance.</summary>
    /// <param name="entity">The entity to load.</param>
    /// <param name="attributeName">The attribute name.</param>
    public static void LoadAttribute(this IEntityInstance entity, string attributeName)
    {
        entity.LoadAttributes(new Collection<string>() { attributeName });
    }

    /// <summary>Loads one attribute on an entity version.</summary>
    /// <param name="entity">The entity to load.</param>
    /// <param name="attributeName">The attribute name.</param>
    public static void LoadAttribute(this IEntityVersion entity, string attributeName)
    {
        entity.LoadAttributes(new Collection<string>() { attributeName });
    }

    /// <summary>Checks whether all specified attributes are present and non-null.</summary>
    /// <param name="obj">The entity to inspect.</param>
    /// <param name="attributeNames">The attribute names to check.</param>
    /// <param name="loadAttributes">Whether to load the attributes before checking.</param>
    /// <returns>True when all specified attributes are present and non-null.</returns>
    public static bool HasAttributes(this IEntity obj, Collection<string> attributeNames, bool loadAttributes = false)
    {
        bool hasAttributes = false;

        if (obj != null && attributeNames != null && attributeNames.Count > 0)
        {
            if (loadAttributes)
            {
                if (obj is IEntityVersion)
                {
                    ((IEntityVersion)obj).LoadAttributes(attributeNames);
                }
                else if (obj is IEntityInstance)
                {
                    ((IEntityInstance)obj).LoadAttributes(attributeNames);
                }
            }
            if (obj.Attributes != null
                && attributeNames.All(attr => obj.Attributes.ContainsKey(attr) && obj.Attributes[attr] != null))
            {
                hasAttributes = true;
            }
        }

        return hasAttributes;
    }

    /// <summary>Checks whether all specified related attributes are present and non-null.</summary>
    /// <param name="obj">The entity version to inspect.</param>
    /// <param name="attributeNames">The related attribute names to check.</param>
    /// <param name="loadAttributes">Whether to load the attributes before checking.</param>
    /// <returns>True when all specified related attributes are present and non-null.</returns>
    public static bool HasRelatedAttributes(this IEntityVersion obj, Collection<string> attributeNames, bool loadAttributes = false)
    {
        bool hasAttributes = false;

        if (obj != null && attributeNames != null && attributeNames.Count > 0)
        {
            if (loadAttributes)
            {
                if (obj is IEntityVersion)
                {
                    obj.LoadAttributes(attributeNames);
                }
                else if (obj is IEntityInstance)
                {
                    ((IEntityInstance)obj).LoadAttributes(attributeNames);
                }
            }
            if (obj.RelatedAttributes != null
                && attributeNames.All(attr => obj.RelatedAttributes.ContainsKey(attr) && obj.RelatedAttributes[attr] != null))
            {
                hasAttributes = true;
            }
        }

        return hasAttributes;
    }

    /// <summary>Merges the entries from two attribute collections into a new collection.</summary>
    /// <param name="attrCollection1">The first collection.</param>
    /// <param name="attrCollection2">The second collection.</param>
    /// <returns>A new collection containing entries from both inputs.</returns>
    public static IAttributeCollection MergeAttributeCollections(this IAttributeCollection attrCollection1, IAttributeCollection attrCollection2)
    {
        IAttributeCollection mergedAttrs = new Cmf.Foundation.BusinessObjects.AttributeCollection();

        if (attrCollection1 != null)
        {
            foreach (var item in attrCollection1)
                mergedAttrs.Add(item.Key, item.Value);
        }

        if (attrCollection2 != null)
        {
            foreach (var item in attrCollection2)
                mergedAttrs.Add(item.Key, item.Value);
        }

        return mergedAttrs;
    }
}
