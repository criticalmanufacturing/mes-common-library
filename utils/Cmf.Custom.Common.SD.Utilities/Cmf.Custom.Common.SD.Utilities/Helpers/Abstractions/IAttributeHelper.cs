using Cmf.Foundation.BusinessObjects.Abstractions;
using System.Collections.ObjectModel;

namespace Cmf.Custom.Common.SD.Utilities.Helpers.Abstractions
{
    /// <summary>
    /// This class contains attributes extension methods
    /// </summary>
    public interface IAttributeHelper
    {
        /// <summary>
        /// This method adds a collection of attributes to a provided entitity.
        /// </summary>
        /// <param name="obj">The object where the attributes will be added.</param>
        /// <param name="attributes">A collection of attributes (AttributeCollection) to add. These attributes are added only if the exist in EntityType.</param>
        void AddAttributesToEntity(IEntityBase obj, IAttributeCollection attributes);

        /// <summary>
        /// This method returns the value stored in the attribute of the provided entity.
        /// </summary>
        /// <param name="obj">The object from which to look for the attribute's value.</param>
        /// <param name="attributeName">A string representing the name of the attribute.</param>
        /// <param name="loadAttribute">A boolean indicating if the attribute should be loaded. Default value is false.</param>
        /// <returns>Returns the attribute's value if it has been found, null otherwise.</returns>
        object GetAttributeValue(IEntity obj, string attributeName, bool loadAttribute = false);


        /// <summary>
        /// Get Attribute Value of an Entity if the attribute exists
        /// </summary>
        /// <typeparam name="T">Type of the Attribute</typeparam>
        /// <param name="entity">Entity to obtain the attribute</param>
        /// <param name="attributeName">Name of the attribute to obtain</param>
        /// <param name="loadAttribute">Should it load the attribute?</param>
        /// <returns>Value of the Attribute</returns>
        T GetAttributeValueOrDefault<T>(IEntity entity, string attributeName, bool loadAttribute = false);

        /// <summary>
        /// Get Attribute Value of an Entity if the attribute exists
        /// </summary>
        /// <typeparam name="T">Type of the Attribute</typeparam>
        /// <param name="entity">Entity to obtain the attribute</param>
        /// <param name="attributeName">Name of the attribute to obtain</param>
        /// <param name="defaultValue">A default value for the attribute, if none define it will use the one of the chosen Type</param>
        /// <param name="loadAttribute">Should it load the attribute?</param>
        /// <returns>Value of the Attribute</returns>
        T GetAttributeValueOrDefault<T>(IEntity entity, string attributeName, T defaultValue, bool loadAttribute = false);



        /// <summary>
        /// This method returns the value stored in the attribute (as a related attribute) of the provided entity.
        /// </summary>
        /// <param name="obj">The object from which to look for the attribute's value.</param>
        /// <param name="attributeName">A string representing the name of the attribute.</param>
        /// <param name="loadAttribute">A boolean indicating if the attribute should be loaded. Default value is false.</param>
        /// <returns>Returns the attribute's value (as a related attribute) if it has been found, null otherwise.</returns>
        object GetRelatedAttributeValue(IEntityVersion obj, string attributeName, bool loadAttribute = false);

        /// <summary>
        /// Get Attribute Value of an EntityRelation if the attribute exists
        /// </summary>
        /// <typeparam name="T">Type of the Attribute</typeparam>
        /// <param name="entity">EntityRelation to obtain the attribute</param>
        /// <param name="attributeName">Name of the attribute to obtain</param>
        /// <param name="loadAttribute">Should it load the attribute?</param>
        /// <returns>Value of the Attribute</returns>
        T GetRelatedAttributeValueOrDefault<T>(IEntityVersion entity, string attributeName, bool loadAttribute = false);

        /// <summary>
        /// Get Related Attribute Value of an Entity if the attribute exists
        /// </summary>
        /// <typeparam name="T">Type of the Attribute</typeparam>
        /// <param name="entity">Entity to obtain the attribute</param>
        /// <param name="attributeName">Name of the attribute to obtain</param>
        /// <param name="defaultValue">Default value in case none is found</param>
        /// <param name="loadAttribute">Should it load the attribute?</param>
        /// <returns>Value of the Attribute</returns>
        T GetRelatedAttributeValueOrDefault<T>(IEntityVersion entity, string attributeName, T defaultValue, bool loadAttribute = false);

        /// <summary>
        /// This method checks if an object has a single attribute that is provided.
        /// </summary>
        /// <param name="obj">The object from which to look for the attribute.</param>
        /// <param name="attributeName">A string representing the name of the Attribute.</param>
        /// <param name="loadAttribute">A boolean indicating if the attribute should be loaded. Default value is false.</param>
        /// <returns>Returns true if the attribute was found, false otherwise.</returns>
        bool HasAttribute(IEntity obj, string attributeName, bool loadAttribute = false);



        /// <summary>
        /// This method checks if an object has a single attribute that is provided as a related attribute.
        /// </summary>
        /// <param name="obj">The object from which to look for the attribute.</param>
        /// <param name="attributeName">A string representing the name of the Attribute to look for.</param>
        /// <param name="loadAttribute">A boolean indicating if the attribute should be loaded. Default value is false.</param>
        /// <returns>Returns true if the provided attribute was found as a related attribute, false otherwise.</returns>
        bool HasRelatedAttribute(IEntityVersion obj, string attributeName, bool loadAttribute = false);



        /// <summary>
        /// This method checks if two entities have the same value for the same attribute.
        /// </summary>
        /// <typeparam name="T">The type of the entities (they both need to share the same type).</typeparam>
        /// <param name="entity1">The first entity to check.</param>
        /// <param name="entity2">The second entity to check.</param>
        /// <param name="attribute">A string representing the name of the Attribute whose value to compare.</param>
        /// <param name="loadAttribute">A boolean indicating if the attribute should be loaded. Default value is false.</param>
        /// <returns>True if the entities have the same value for the provided attribute, false otherwise.</returns>
        bool HasSameAttributeValue<T>(T entity1, T entity2, string attribute, bool loadAttribute = true) where T : IEntity;




    }
}
