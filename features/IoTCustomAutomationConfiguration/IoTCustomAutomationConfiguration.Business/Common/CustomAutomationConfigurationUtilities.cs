using Cmf.Common.CustomActionUtilities;
using Cmf.Foundation.BusinessObjects;
using Cmf.Foundation.BusinessObjects.Abstractions;
using Cmf.Foundation.BusinessObjects.Cultures;
using Cmf.Foundation.BusinessObjects.QueryObject;
using Cmf.Foundation.BusinessObjects.SmartTables;
using Cmf.Foundation.Common;
using Cmf.Foundation.Common.Abstractions;
using Cmf.Foundation.Configuration;
using Cmf.Navigo.BusinessObjects;
using Cmf.Navigo.BusinessObjects.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data;
using System.Globalization;
using System.IO.Enumeration;
using System.Linq;
using System.Threading;


namespace Cmf.Community.IoTCustomAutomationConfiguration.Actions.Common
{
    public static class CustomAutomationConfigurationUtilities
    {
        #region Generic

        /// <summary>
        /// Get Value as nullable decimal.
        /// </summary>
        /// <param name="value">Value to be converted.</param>
        /// <returns></returns>
        public static decimal? GetValueAsNullableDecimal(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            return decimal.Parse(value.Replace(",", CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator).Replace(".", CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator), NumberStyles.Number | NumberStyles.AllowExponent);
        }

        /// <summary>
        /// Get Value as nullable boolean
        /// </summary>
        /// <param name="value">Value to be converted.</param>
        /// <returns></returns>
        public static bool? GetValueAsNullableBoolean(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            // True: Possible values 
            string[] positiveValues = { "y", "true", "yes", "1" };

            if (positiveValues.Contains(value.Trim(), StringComparer.InvariantCultureIgnoreCase))
            {
                return true;
            }

            // False: Possible values
            string[] negativeValues = { "n", "false", "no", "0" };

            if (negativeValues.Contains(value.Trim(), StringComparer.InvariantCultureIgnoreCase))
            {
                return false;
            }

            return default(bool?);
        }

        /// <summary>
        /// Get Value as decimal.
        /// </summary>
        /// <param name="value">Value to be converted.</param>
        /// <returns></returns>
        public static decimal GetValueAsDecimal(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return default(decimal);
            }

            return decimal.Parse(value.Replace(",", CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator).Replace(".", CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator), NumberStyles.Number | NumberStyles.AllowExponent);
        }

        /// <summary>
        /// Get Value as boolean.
        /// </summary>
        /// <param name="value">Value to be converted.</param>
        /// <returns></returns>
        public static bool GetValueAsBoolean(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            string[] booleanValues = { "y", "true", "yes", "1" };

            if (booleanValues.Contains(value.Trim(), StringComparer.InvariantCultureIgnoreCase))
            {
                return true;
            }

            return default(bool);
        }

        /// <summary>
        /// Gets the value as enum.
        /// </summary>
        /// <typeparam name="T">Type of the enum.</typeparam>
        /// <param name="value">Value to be converted.</param>
        /// <returns>Return the value as enum value.</returns>
        public static T GetValueAsEnum<T>(string value) where T : struct
        {
            T result;

            if (Enum.TryParse<T>(value, out result))
            {
                return result;
            }

            return default(T);
        }

        /// <summary>
        /// Determines whether the collection is null or contains no elements.
        /// </summary>
        /// <typeparam name="T">The IEnumerable type.</typeparam>
        /// <param name="enumerable">The enumerable, which may be null or empty.</param>
        /// <returns>
        ///     <c>true</c> if the IEnumerable is null or empty; otherwise, <c>false</c>.
        /// </returns>
        public static bool IsNullOrEmpty<T>(this IEnumerable<T> enumerable)
        {
            if (enumerable == null)
            {
                return true;
            }

            /* If this is a list, use the Count property for efficiency.
			 * The Count property is O(1) while IEnumerable.Count() is O(N). */
            var collection = enumerable as ICollection<T>;

            if (collection != null)
            {
                return collection.Count < 1;
            }

            return !enumerable.Any();
        }

        /// <summary>
        /// Get Value as dynamic DataType.
        /// </summary>
        /// <param name="parameterDataType">Parameter Data Type.</param>
        /// <param name="value">Value to be converted.</param>
        /// <returns></returns>
        public static dynamic GetParameterValueAsDataType(ParameterDataType parameterDataType, string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return default(dynamic);
            }

            switch (parameterDataType)
            {
                case ParameterDataType.Decimal:
                    return GetValueAsNullableDecimal(value);

                case ParameterDataType.Boolean:
                    return GetValueAsNullableBoolean(value);

                default:
                    return value;
            }
        }


        /// <summary>
        /// Get Value as dynamic DataType.
        /// </summary>
        /// <param name="scalarType">Scalar Type.</param>
        /// <param name="value">Value to be converted.</param>
        /// <returns></returns>
        public static dynamic GetAttributeValueAsDataType(ScalarType scalarType, string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return default(dynamic);
            }

            switch (scalarType.NativeType)
            {
                case "System.Decimal":
                    return GetValueAsNullableDecimal(value);

                case "System.Boolean":
                    return GetValueAsNullableBoolean(value);

                default:
                    return value;
            }
        }


        #endregion

        #region Configs

        #endregion Configs

        #region SmartTables       
        /// <summary>
        /// Method to resolve Material Data Collection Context 
        /// </summary>
        /// <param name="step"></param>
        /// <param name="logicalFlowPath"></param>
        /// <param name="product"></param>
        /// <param name="productGroup"></param>
        /// <param name="flow"></param>
        /// <param name="material"></param>
        /// <param name="materialType"></param>
        /// <param name="resource"></param>
        /// <param name="resourceType"></param>
        /// <param name="model"></param>
        /// <param name="operation"></param>
        /// <returns></returns>
        public static INgpDataSet CustomResolveMaterialDataCollectionContext(string step = null,
                                                                            string logicalFlowPath = null,
                                                                            string product = null,
                                                                            string productGroup = null,
                                                                            string flow = null,
                                                                            string material = null,
                                                                            string materialType = null,
                                                                            string resource = null,
                                                                            string resourceType = null,
                                                                            string model = null,
                                                                            string operation = null)
        {
            Dictionary<string, string> result = new Dictionary<string, string>();
            ISmartTable materialDatacollectionContext = new SmartTable();
            materialDatacollectionContext.Load(Cmf.Navigo.Common.Constants.MaterialDataCollectionContext);

            INgpDataRow values = new NgpDataRow();

            // If step name is filled apply it as a filter
            if (!string.IsNullOrWhiteSpace(step))
            {
                values.Add(Cmf.Navigo.Common.Constants.Step, step);
            }

            // If logical flow path is filled apply it as a filter
            if (!string.IsNullOrWhiteSpace(logicalFlowPath))
            {
                values.Add("LogicalFlowPath", logicalFlowPath);
            }

            // If product name is filled apply it as a filter
            if (!string.IsNullOrWhiteSpace(product))
            {
                values.Add(Cmf.Navigo.Common.Constants.Product, product);
            }

            // If product group is filled apply it as a filter
            if (!string.IsNullOrWhiteSpace(productGroup))
            {
                values.Add(Cmf.Navigo.Common.Constants.ProductGroup, productGroup);
            }

            // If flow name is filled apply it as a filter
            if (!string.IsNullOrWhiteSpace(flow))
            {
                values.Add(Cmf.Navigo.Common.Constants.Flow, flow);
            }

            // If lot name is filled apply it as a filter
            if (!string.IsNullOrWhiteSpace(material))
            {
                values.Add(Cmf.Navigo.Common.Constants.Material, material);
            }

            // If lot type is filled apply it as a filter
            if (!string.IsNullOrWhiteSpace(materialType))
            {
                values.Add(Cmf.Navigo.Common.Constants.MaterialType, materialType);
            }

            // If resource name is filled apply it as a filter
            if (!string.IsNullOrWhiteSpace(resource))
            {
                values.Add(Cmf.Navigo.Common.Constants.Resource, resource);
            }

            // If resource type is filled apply it as a filter
            if (!string.IsNullOrWhiteSpace(resourceType))
            {
                values.Add(Cmf.Navigo.Common.Constants.ResourceType, resourceType);
            }

            // If model is filled apply it as a filter
            if (!string.IsNullOrWhiteSpace(model))
            {
                values.Add(Cmf.Navigo.Common.Constants.Model, model);
            }

            // If operation name is filled apply it as a filter
            if (!string.IsNullOrWhiteSpace(operation))
            {
                values.Add(Cmf.Navigo.Common.Constants.Operation, operation);
            }

            INgpDataSet materialDCContextNgpDataSet = materialDatacollectionContext.Resolve(values, true);



            return materialDCContextNgpDataSet;
        }



        #endregion



        #region Data Collection

        /// <summary>
        /// Method to validate if one of the posted points do not respect the configured limit set 
        /// </summary>
        /// <param name="dataCollectionInstance"></param>
        /// <returns></returns>
        public static bool IsDataCollectionLimiSetViolated(DataCollectionInstance dataCollectionInstance)
        {
            dataCollectionInstance.LoadRelations("DataCollectionPoint");

            IDataCollectionLimitSet dataCollectionLimitSet = dataCollectionInstance.DataCollectionLimitSet;

            IDataCollectionPointCollection dataCollectionPoints = dataCollectionInstance.DataCollectionPoints;



            foreach (IDataCollectionParameterLimit parameterLimit in dataCollectionLimitSet.DataCollectionParameterLimits)
            {
                IDataCollectionPoint dcPoint = dataCollectionPoints.FirstOrDefault(dcp => dcp.GetNativeValue<long>(Constants.TargetEntity).Equals(parameterLimit.GetNativeValue<long>(Constants.TargetEntity)));

                decimal value = Convert.ToDecimal(dcPoint.Value);

                if ((parameterLimit.UpperErrorLimit != null && value > parameterLimit.UpperErrorLimit) || (parameterLimit.LowerErrorLimit != null && value < parameterLimit.LowerErrorLimit))
                {
                    return true;
                }
            }

            return false;
        }

        #endregion

        #region DEEActionUtilities
        /// <summary>
        /// Checks if current action group (present in Input dicionary) is valid based on list of given action groups
        /// </summary>
        /// <param name="Input">Dictionary where in theory action group is defined</param>
        /// <param name="ValidActionGroups">List of valid action groups</param>
        /// <param name="defaultBehavior">default behavior of method. Defaults to false but can be changed by invoker</param>
        /// <returns></returns>
        public static bool IsActionGroupValid(Dictionary<string, object> Input, Collection<string> ValidActionGroups, bool defaultBehavior = false)
        {
            // by default action group is not valid unless explicitly defined otherwise
            bool isValid = defaultBehavior;

            // proceed if Action group name can be extracted from Input
            string actionGroupName = GetActionGroup(Input);
            if (!String.IsNullOrWhiteSpace(actionGroupName) && ValidActionGroups != null && ValidActionGroups.Any())
            {
                isValid = ValidActionGroups.Any(E => String.Equals(E, actionGroupName, StringComparison.InvariantCultureIgnoreCase));
            }

            return isValid;
        }

        /// <summary>
        /// Retrieves the action group
        /// </summary>
        /// <param name="Input"></param>
        /// <returns></returns>
        public static string GetActionGroup(Dictionary<string, object> Input)
        {
            string returnValue = GetInputItem<string>(Input, "ActionGroupName", String.Empty);

            return returnValue;
        }

        /// <summary>
        /// Tries to retrieve a given item from the Input dictionary, based on the item name and passed type
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="Input"></param>
        /// <param name="inputEntryName"></param>
        /// <param name="defaultValue"></param>
        /// <returns></returns>
        public static T GetInputItem<T>(Dictionary<string, object> Input, string inputEntryName, T defaultValue = default(T))
        {
            // define return value
            T returnObject = defaultValue;

            // try to extract the element
            object oExtractedItem = null;
            if (Input.TryGetValue(inputEntryName, out oExtractedItem) && oExtractedItem is T)
            {
                returnObject = (T)oExtractedItem;
            }

            return returnObject;
        }

        public static void SetMaterialStateModel(Material material, string stateModelName, string state)
        {
            // Get Entity Factory
            var services = ApplicationContext.CurrentServiceProvider;
            IEntityFactory entityFactory = services.GetService<IEntityFactory>();

            if (material.CurrentMainState == null
                || !material.CurrentMainState.CurrentState.Name.Equals(state))
            {
                StateModel stateModel = new StateModel()
                {
                    Name = stateModelName
                };

                stateModel.Load();
                StateModelState stateModelState = new StateModelState();
                stateModelState.Load(state, stateModel);

                ICurrentEntityState currentEntityState = entityFactory.Create<ICurrentEntityState>();
                currentEntityState.Entity = material;
                currentEntityState.StateModel = stateModel;
                currentEntityState.CurrentState = stateModelState;
                material.SetMainStateModel(currentEntityState);
            }
        }

        /// <summary>
        /// Gets the Entity Attributes Definition (Name; Type)
        /// </summary>
        /// <param name="entityName">The Entity Name.</param>
        /// <returns>A Dictionary of the Attributes Definition.</returns>
        public static Dictionary<string, object> GetEntityAttributesDefinition(string entityName)
        {
            Dictionary<string, object> attributes = new Dictionary<string, object>();

            EntityType entityType = new EntityType();

            entityType.Load(entityName);

            entityType.LoadProperties();

            if (entityType.Properties != null && entityType.Properties.Any())
            {
                IEnumerable<IEntityTypeProperty> attributesDefinition = entityType.Properties.Where(w => w.PropertyType == EntityTypePropertyType.Attribute);

                if (attributesDefinition != null && attributesDefinition.Any())
                {
                    attributes = attributesDefinition.Select(s => new KeyValuePair<string, object>(s.Name, s.ScalarType)).ToDictionary(d => d.Key, d => d.Value);
                }
            }

            return attributes;
        }


        #endregion

        #region Localized Messages

        /// <summary>
        /// Constructs a new Message, using Localized Messages
        /// </summary>
        /// <param name="key"></param>
        /// <param name="parameters"></param>
        /// <returns></returns>
        public static string GetLocalizedMessage(string key, params string[] parameters)
        {
            // ILocalizedMessage localizedMessageObj = LocalizedMessage.GetLocalizedMessage(Thread.CurrentThread.CurrentCulture.Name, key);
            // return string.Format(localizedMessageObj.MessageText, parameters);
            return CustomAutomationConfigurationUtilities.GetLocalizedMessage(key, parameters);
        }

        /// <summary>
        /// Constructs a new Exception Message, using Localized Messages
        /// </summary>
        /// <param name="key"></param>
        /// <param name="parameters"></param>
        /// <returns></returns>
        public static void ThrowLocalizedException(string key, params string[] parameters)
        {
            string exceptionMessage = CustomAutomationConfigurationUtilities.GetLocalizedMessage(key, parameters);

            throw new Exception(exceptionMessage);
        }

        #endregion Localized Messages

        #region Resource
        #endregion Resource

    }

}
