using Cmf.Custom.Common.SD.Utilities.Helpers.Abstractions;
using Cmf.Custom.Common.SD.Utilities.Extensions;
using Cmf.Custom.Common.SD.Utilities.DataStructures;
using Cmf.Foundation.BusinessObjects;
using Cmf.Foundation.BusinessObjects.Abstractions;
using Cmf.Foundation.BusinessOrchestration;
using Cmf.Foundation.BusinessOrchestration.Abstractions;
using Cmf.Foundation.BusinessOrchestration.DynamicExecutionEngineManagement.InputObjects;
using Cmf.Foundation.Common.Abstractions;
using Cmf.Navigo.BusinessObjects.Abstractions;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Reflection;

namespace Cmf.Custom.Common.SD.Utilities.Helpers
{
    /// <summary>
    /// This is a utility class for DEE Actions.
    /// </summary>
    public class DEEHelper : IDEEHelper
    {
        #region Private Variables

        private readonly IDynamicExecutionEngineOrchestration _dynamicExecutionEngineOrchestration;
        private readonly ICallContext _callContext;
        private readonly IEntityHelper _entityHelper;

        #endregion Private Variables

        /// <summary>
        /// Initializes a new instance of the <see cref="DEEHelper"/> class.
        /// </summary>
        [Microsoft.Extensions.DependencyInjection.ActivatorUtilitiesConstructor]
        public DEEHelper(IDynamicExecutionEngineOrchestration dynamicExecutionEngineOrchestration, ICallContext callContext, IEntityHelper entityHelper)
        {
            this._dynamicExecutionEngineOrchestration = dynamicExecutionEngineOrchestration;
            this._callContext = callContext;
            this._entityHelper = entityHelper;
        }

        #region Action Group

        /// <inheritdoc/>
        public bool IsActionGroupValid(Dictionary<string, object> Input, Collection<string> ValidActionGroups, bool defaultBehavior = false)
        {
            // by default action group is not valid unless explicitly defined otherwise
            bool isValid = defaultBehavior;

            // proceed if Action group name can be extracted from Input
            string actionGroupName = GetActionGroup(Input);
            if (!string.IsNullOrWhiteSpace(actionGroupName) && ValidActionGroups != null && ValidActionGroups.Any())
            {
                isValid = ValidActionGroups.Any(E => string.Equals(E, actionGroupName, StringComparison.InvariantCultureIgnoreCase));
            }

            return isValid;
        }

        /// <inheritdoc/>
        public string GetActionGroup(Dictionary<string, object> Input)
        {
            string returnValue = Input.GetInputItem<string>("ActionGroupName", string.Empty);

            return returnValue;
        }



        /// <inheritdoc/>
        public Dictionary<string, object> ExecuteAction(string actionName, Dictionary<string, object> input)
        {
            GetActionByNameInput deeInput = new GetActionByNameInput
            {
                Name = actionName
            };

            Cmf.Foundation.Common.Abstractions.IAction action = this._dynamicExecutionEngineOrchestration.GetActionByName(deeInput).Action;

            ExecuteActionInput execute = new ExecuteActionInput()
            {
                Action = action,
                Input = input
            };

            return this._dynamicExecutionEngineOrchestration.ExecuteAction(execute).Output;
        }

        #endregion Action Group

        #region Service Context

        /// <inheritdoc/>
        public DeeContext SetCurrentServiceContext(string deeRuleName, bool skipInitialContext = false)
        {
            DeeContext context = new DeeContext();

            if (context.TriggerPoint == DeeTriggerPoint.Unknown)
            {
                // Get context info from the call Stack
                StackTrace stack = new StackTrace();

                foreach (StackFrame frame in stack.GetFrames())
                {
                    if (frame != null)
                    {
                        string method = frame.GetMethod().Name;

                        if (context.TriggerPoint == DeeTriggerPoint.Unknown)
                        {
                            if (method == "StartMethod")
                                context.TriggerPoint = DeeTriggerPoint.Pre;
                            else if (method == "EndMethod")
                                context.TriggerPoint = DeeTriggerPoint.Post;
                        }
                        else if (method != "StartMethod" && method != "EndMethod")
                        {
                            context.MethodName = method;
                            break;
                        }
                    }
                }
            }

            SetContextParameter(deeRuleName + "_CurrentServiceContext", context);

            if (!skipInitialContext && GetInitialServiceContext(deeRuleName) == null)
            {
                SetContextParameter(deeRuleName + "_InitialServiceContext", context);
            }

            return context;
        }

        /// <inheritdoc/>
        public DeeContext GetCurrentServiceContext(string deeRuleName)
        {
            return GetContextParameter(deeRuleName + "_CurrentServiceContext") as DeeContext;
        }

        /// <inheritdoc/>
        public DeeContext GetInitialServiceContext(string deeRuleName)
        {
            return GetContextParameter(deeRuleName + "_InitialServiceContext") as DeeContext;
        }

        /// <inheritdoc/>
        public String GetServiceComments()
        {
            String value = null;

            if (_callContext != null)
            {
                value = _callContext.GetHistoryCollection().ServiceComment;
            }

            return value;
        }

        #endregion Service Context

        #region Parameters

        /// <inheritdoc/>
        public void SetContextParameter(string key, object value)
        {
            if (_callContext != null)
            {
                _callContext.SetInformationContext(key, value);
            }
        }

        /// <inheritdoc/>
        public object GetContextParameter(string key)
        {
            return GetContextParameter<object>(key);
        }

        /// <inheritdoc/>
        public T GetContextParameter<T>(string key)
        {
            return (T)(_callContext?.GetInformationContext(key) ?? default(T));
        }

        #endregion Parameters

        #region Generic

        ///<inheritdoc/>
        public IMaterial GetMaterialFromDeeInput(Dictionary<String, Object> deeInput, bool isInPreconditionCheck = false, bool skipServiceInputs = false, bool skipServiceOutputs = false, bool skipOperations = false, bool loadShallowObjects = true, int searchDepth = 10)
        {
            IMaterial material = GetObjectFromDeeInput<IMaterial>(deeInput, skipServiceInputs, skipServiceOutputs, skipOperations, searchDepth);

            #region Reload Material if in shallow object form

            if (loadShallowObjects && material != null && material.Id > 0 && material.Facility == null)
            {

                material = material.ReloadEntity<IMaterial>();
            }
            #endregion

            return material;
        }

        ///<inheritdoc/>
        public T GetObjectFromDeeInput<T>(Dictionary<String, Object> deeInput, bool skipServiceInputs = false, bool skipServiceOutputs = false, bool skipOperations = false, int searchDepth = 10) where T : class
        {
            T obj = null;

            if (!skipServiceOutputs)
            {
                // Check standard orchestration output objects
                obj = GetObjectFromStructure<T>(deeInput.GetServiceOutputObject(), null, searchDepth);
            }

            if (!skipServiceInputs && obj == null)
            {
                // Check standard orchestration input objects
                obj = GetObjectFromStructure<T>(deeInput.GetServiceInputObject(), null, searchDepth);
            }

            if (!skipOperations && obj == null)
            {
                // Check standard operation objects
                foreach (KeyValuePair<String, Object> param in deeInput)
                {
                    if (!(param.Value is BaseInput || param.Value is BaseOutput))
                    {
                        obj = GetObjectFromStructure<T>(param.Value, null, searchDepth);

                        if (obj != null)
                        {
                            break;
                        }
                    }
                }
            }

            return obj;
        }

        ///<inheritdoc/>
        public T GetObjectFromStructure<T>(dynamic objStructure, string[] propertyNames = null, int searchDepth = 10) where T : class
        {
            T obj = null;

            if ((objStructure is ValueType || objStructure != null) && searchDepth > 0)
            {
                searchDepth--;

                if (propertyNames == null)
                    propertyNames = new string[0];

                if (propertyNames.Length == 0 && objStructure is T)
                {
                    // Match found
                    obj = objStructure;
                }
                else if (propertyNames.Length == 0
                    && (objStructure is T[] && objStructure.Length > 0) || (objStructure is IList<T> && objStructure.Count > 0))
                {
                    // Match found in array
                    obj = objStructure[0];
                }
                else if (propertyNames.Length > 0
                        && (objStructure is Dictionary<string, T> || objStructure is Dictionary<string, object>))
                {
                    foreach (string propName in propertyNames)
                    {
                        if (objStructure.ContainsKey(propName))
                        {
                            // Match by name found in Dictionary
                            obj = objStructure[propName] as T;
                        }
                    }
                }
                else if (objStructure is IDictionary || objStructure is IList || objStructure is ICollection
                         || objStructure is IEnumerable || objStructure is Array)
                {
                    // Recursively check each Collection element
                    foreach (var item in objStructure)
                    {
                        obj = GetObjectFromStructure<T>(item, propertyNames, searchDepth);

                        if (obj != null)
                            break;
                    }
                }
                else
                {
                    PropertyInfo[] props = objStructure.GetType().GetProperties();

                    if (props != null)
                    {
                        PropertyInfo propMatch = null;
                        foreach (string propName in propertyNames)
                        {
                            propMatch = props.FirstOrDefault(p => p.Name == propName);

                            if (propMatch != null)
                            {
                                // Match by name found in Property
                                obj = ReflectionExtensions.TryGetPropertyValue(propMatch, (object)objStructure) as T;
                                break;
                            }
                        }

                        if (propMatch == null)
                        {
                            // Recursively check each property of the objectStructure class
                            foreach (PropertyInfo prop in props)
                            {
                                obj = GetObjectFromStructure<T>(ReflectionExtensions.TryGetPropertyValue(prop, (object)objStructure), propertyNames, searchDepth);

                                if (obj != null)
                                    break;
                            }
                        }
                    }
                }
            }

            return obj;
        }

        ///<inheritdoc/>
        public String GetServiceComments(Dictionary<String, Object> deeInput)
        {
            BaseInput serviceInput = deeInput.GetServiceInputObject();

            if (serviceInput != null)
            {
                return serviceInput.ServiceComments;
            }
            return null;
        }









        ///<inheritdoc/>
        public IAttributeCollection ResolveFinalAttributeValues(IAttributeCollection originalAttrs, BaseInput serviceInput)
        {
            IAttributeCollection finalAttrs = originalAttrs ?? new AttributeCollection();

            IAttributeCollection updatedAttrs = GetUpdatedAttributes(serviceInput);
            Collection<string> removedAttrNames = GetRemovedAttributeNames(serviceInput);

            if (removedAttrNames != null && removedAttrNames.Count > 0)
            {
                foreach (string attrName in removedAttrNames)
                {
                    if (finalAttrs.ContainsKey(attrName))
                        finalAttrs.Remove(attrName);
                }
            }

            foreach (var attr in updatedAttrs)
            {
                if (finalAttrs.ContainsKey(attr.Key))
                    finalAttrs[attr.Key] = attr.Value;
                else
                    finalAttrs.Add(attr.Key, attr.Value);
            }

            return finalAttrs;
        }

        ///<inheritdoc/>
        public IAttributeCollection GetUpdatedAttributes(BaseInput serviceInput)
        {
            IAttributeCollection updatedAttrs = new AttributeCollection();

            if (serviceInput != null)
            {
                string inputType = serviceInput.GetType().Name;

                if (inputType.ToLower().Contains("fullupdate"))
                {
                    //FullUpdateMaterial/FullUpdateObject.FullUpdateParameters: AttributesToAddOrUpdate, AttributesNamesToRemove, [RelatedAttributesToAddOrUpdate, RelatedAttributesNamesToRemove]
                    // In Material, Service, SortRule and Reason, it's "AttributesToAddorUpdate" with the "or" in lowercase :(
                    string[] possiblePropertyNames = new string[] { "AttributesToAddOrUpdate", "AttributesToAddorUpdate" };

                    updatedAttrs = GetObjectFromStructure<IAttributeCollection>(serviceInput, possiblePropertyNames);

                    // get definition attributes for versioned objects
                    possiblePropertyNames = new string[] { "RelatedAttributesToAddOrUpdate" };
                    IAttributeCollection updatedDefAttrNames = GetObjectFromStructure<IAttributeCollection>(serviceInput, possiblePropertyNames);

                    // merge definition with version attribute Names
                    updatedAttrs = updatedAttrs.MergeAttributeCollections(updatedDefAttrNames);
                }
                else if (inputType.ToLower().Contains("update"))
                {
                    //UpdateObjectsAttributes: AttributeCollection, [RelatedAttributeCollection]
                    //UpdateMaterialAttributes: Attributes
                    string[] possiblePropertyNames = new string[] { "AttributeCollection", "Attributes" };

                    updatedAttrs = GetObjectFromStructure<IAttributeCollection>(serviceInput, possiblePropertyNames);

                    // get definition attributes for versioned objects
                    possiblePropertyNames = new string[] { "RelatedAttributeCollection" };
                    IAttributeCollection updatedDefAttrNames = GetObjectFromStructure<IAttributeCollection>(serviceInput, possiblePropertyNames);

                    // merge definition with version attribute Names
                    updatedAttrs = updatedAttrs.MergeAttributeCollections(updatedDefAttrNames);
                }
            }

            return updatedAttrs;
        }

        ///<inheritdoc/>
        public Collection<string> GetRemovedAttributeNames(BaseInput serviceInput)
        {
            Collection<string> removedAttrNames = new Collection<string>();

            if (serviceInput != null)
            {
                string inputType = serviceInput.GetType().Name;

                if (inputType.ToLower().Contains("fullupdate"))
                {
                    //FullUpdateMaterial/FullUpdateObject.FullUpdateParameters: AttributesToAddorUpdate, AttributesNamesToRemove, [RelatedAttributesToAddorUpdate, RelatedAttributesNamesToRemove]
                    string[] possiblePropertyNames = new string[] { "AttributesNamesToRemove", "AttributeNamesToRemove" };

                    removedAttrNames = GetObjectFromStructure<Collection<string>>(serviceInput, possiblePropertyNames);

                    // get definition attributes for versioned objects
                    possiblePropertyNames = new string[] { "RelatedAttributesNamesToRemove" };
                    Collection<string> removedDefAttrNames = GetObjectFromStructure<Collection<string>>(serviceInput, possiblePropertyNames);

                    if (removedDefAttrNames != null)
                    {
                        // merge definition with version attribute Names
                        if (removedAttrNames != null)
                        {
                            foreach (string attrName in removedDefAttrNames)
                            {
                                removedAttrNames.Add(attrName);
                            }
                        }
                        else
                        {
                            removedAttrNames = removedDefAttrNames;
                        }
                    }
                }
                else if (inputType.ToLower().Contains("remove"))
                {
                    //RemoveMaterialAttributes/RemoveObjectAttributes/RemoveObjectRelatedAttributes: AttributeNames (string)
                    string[] possiblePropertyNames = new string[] { "AttributeNames" };

                    removedAttrNames = GetObjectFromStructure<Collection<string>>(serviceInput, possiblePropertyNames);
                }
            }
            return removedAttrNames;
        }

        ///<inheritdoc/>
        public Collection<string> GetUpdatedAndRemovedAttributeNames(BaseInput serviceInput)
        {
            Collection<string> updatedAndRemovedAttrNames = new Collection<string>();

            IAttributeCollection updatedAttrs = GetUpdatedAttributes(serviceInput);
            if (updatedAttrs != null)
                foreach (string item in updatedAttrs.Select(a => a.Key))
                    updatedAndRemovedAttrNames.Add(item);

            Collection<string> removedAttrNames = GetRemovedAttributeNames(serviceInput);
            if (removedAttrNames != null)
                foreach (string item in removedAttrNames)
                    updatedAndRemovedAttrNames.Add(item);

            return updatedAndRemovedAttrNames;
        }





        #endregion Generic

    }
}
