using Cmf.Custom.Common.SD.Utilities.Helpers.Abstractions;
using Cmf.Custom.Common.SD.Utilities.Extensions;
using Cmf.Foundation.BusinessObjects;
using Cmf.Foundation.BusinessObjects.Abstractions;
using Cmf.Foundation.BusinessOrchestration.Abstractions;
using Cmf.Foundation.BusinessOrchestration.EntityTypeManagement.InputObjects;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace Cmf.Custom.Common.SD.Utilities.Helpers
{
    ///<inheritdoc/>
    public class AttributeHelper : IAttributeHelper
    {
        #region Private Variables
        private readonly IEntityTypeOrchestration _entityTypeOrchestration;

        #endregion Private Variables

        /// <summary>
        /// Initializes a new instance of the <see cref="AttributeHelper"/> class.
        /// </summary>
        [Microsoft.Extensions.DependencyInjection.ActivatorUtilitiesConstructor]
        public AttributeHelper(IEntityTypeOrchestration entityTypeOrchestration)
        {
            _entityTypeOrchestration = entityTypeOrchestration;
        }


        ///<inheritdoc/>
        public T GetAttributeValueOrDefault<T>(IEntity entity, string attributeName, T defaultValue, bool loadAttribute = false)
        {
            T attributeValue = defaultValue;

            if (HasAttribute(entity, attributeName, loadAttribute))
            {
                attributeValue = (T)GetAttributeValue(entity, attributeName);
            }

            return attributeValue;
        }

        ///<inheritdoc/>
        public T GetAttributeValueOrDefault<T>(IEntity entity, string attributeName, bool loadAttribute = false)
        {
            return GetAttributeValueOrDefault(entity, attributeName, default(T), loadAttribute);
        }

        ///<inheritdoc/>
        public T GetRelatedAttributeValueOrDefault<T>(IEntityVersion entity, string attributeName, bool loadAttribute = false)
        {
            return GetRelatedAttributeValueOrDefault(entity, attributeName, default(T), loadAttribute);
        }

        ///<inheritdoc/>
        public T GetRelatedAttributeValueOrDefault<T>(IEntityVersion entity, string attributeName, T defaultValue, bool loadAttribute = false)
        {
            T attributeValue = defaultValue;

            if (HasRelatedAttribute(entity, attributeName, loadAttribute))
            {
                attributeValue = (T)entity.RelatedAttributes[attributeName];
            }

            return attributeValue;
        }









        ///<inheritdoc/>
        public bool HasAttribute(IEntity obj, string attributeName, bool loadAttribute = false)
        {
            return obj.HasAttributes(new Collection<string> { attributeName }, loadAttribute);
        }



        ///<inheritdoc/>
        public bool HasSameAttributeValue<T>(T entity1, T entity2, string attribute, bool loadAttribute = true) where T : IEntity
        {
            bool sameAttriibuteValue = false;

            if (HasAttribute(entity2, attribute, loadAttribute))
            {
                sameAttriibuteValue = entity1.Attributes[attribute].Equals(entity2.Attributes[attribute]);
            }

            return sameAttriibuteValue;
        }

        ///<inheritdoc/>
        public bool HasRelatedAttribute(IEntityVersion obj, string attributeName, bool loadAttribute = false)
        {
            return obj.HasRelatedAttributes(new Collection<string> { attributeName }, loadAttribute);
        }

        ///<inheritdoc/>
        public object GetAttributeValue(IEntity obj, string attributeName, bool loadAttribute = false)
        {
            return HasAttribute(obj, attributeName, loadAttribute) ? obj.Attributes[attributeName] : null;
        }

        ///<inheritdoc/>
        public object GetRelatedAttributeValue(IEntityVersion obj, string attributeName, bool loadAttribute = false)
        {
            return HasRelatedAttribute(obj, attributeName, loadAttribute) ? obj.RelatedAttributes[attributeName] : null;
        }

        ///<inheritdoc/>
        public void AddAttributesToEntity(IEntityBase obj, IAttributeCollection attributes)
        {
            if (attributes != null && attributes.Count > 0)
            {
                IEntityType entityType = _entityTypeOrchestration.GetEntityTypeByName(new GetEntityTypeByNameInput
                {
                    Name = obj.GetType().Name
                }).EntityType;

                foreach (KeyValuePair<string, object> attribute in attributes)
                {
                    EntityTypeProperty property = (EntityTypeProperty)entityType.Properties.FirstOrDefault(x => x.Name == attribute.Key && x.PropertyType == EntityTypePropertyType.Attribute);
                    if (property != null)
                    {
                        if (entityType.IsVersioned)
                        {
                            if (property.BelongsTo == EntityTypeSource.Revision)
                            {
                                (obj as IEntityVersion).RelatedAttributes[attribute.Key] = attribute.Value;
                            }
                            else if (property.BelongsTo == EntityTypeSource.Version)
                            {
                                obj.Attributes[attribute.Key] = attribute.Value;
                            }
                        }
                        else
                        {
                            obj.Attributes[attribute.Key] = attribute.Value;
                        }
                    }
                }
            }
        }

    }
}
