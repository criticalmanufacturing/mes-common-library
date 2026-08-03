using Cmf.Foundation.BusinessObjects;
using Cmf.Foundation.BusinessObjects.Abstractions;
using Cmf.Foundation.BusinessOrchestration;
using Cmf.Foundation.Common;
using Cmf.Foundation.Common.Abstractions;
using Cmf.Navigo.BusinessObjects;
using Cmf.Navigo.BusinessObjects.Abstractions;
using Cmf.Navigo.BusinessOrchestration.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reflection;
using Cmf.Foundation.Configuration.Abstractions;
using Cmf.Foundation.BusinessObjects.SmartTables;
using System.IO;
using Cmf.Foundation.BusinessOrchestration.Abstractions;
using Cmf.Foundation.BusinessOrchestration.EntityTypeManagement.InputObjects;
using Cmf.Community.IoTCustomAutomationConfiguration.Actions.Orchestration.OutputObjects;
using Cmf.Community.IoTCustomAutomationConfiguration.Actions.Orchestration.InputObjects;
using Cmf.Community.IoTCustomAutomationConfiguration.Actions.Common;
using Cmf.Foundation.Configuration;
using Cmf.Community.IoTCustomAutomationConfiguration.Actions.Common.Extensions;
using System.Data;
using Cmf.Community.IoTCustomAutomationConfiguration.Actions.Common.DataStructures;
using Cmf.Foundation.BusinessOrchestration.ConfigurationManagement.InputObjects;

namespace Cmf.Community.IoTCustomAutomationConfiguration.Actions.Orchestration
{
    public class CustomAutomationConfigurationOrchestration : ICustomAutomationConfigurationOrchestration
    {
        // Entity Factory
        private readonly IEntityFactory _entityFactory;

        // Orchestrations
        private readonly IMaterialOrchestration _materialOrchestration;
        private readonly IResourceOrchestration _resourceOrchestration;
        private readonly IExceptionProtocol _exceptionProtocol;
        private readonly IDispatchOrchestration _dispatchOrchestration;
        private readonly IEntityTypeOrchestration _entityTypeOrchestration;
        private readonly IConfigurationOrchestration _configurationOrchestration;
        private readonly IGenericServiceOrchestration _genericServiceOrchestration;


        private const string OBJECT_TYPE_NAME = "Cmf.Community.IoTCustomAutomationConfiguration.Actions.Orchestration";

        //injected types
        private Type iCustomAutomationConfiguration;
        private Dictionary<string, Type> iCustomAutomationConfigurationEntities = new Dictionary<string, Type>();
        private Type iCustomAutomationConfigurationStructure;
        private Type iCustomAutomationConfigurationValue;

        private Type iCustomAutomationConfigurationCollection;
        private Dictionary<string, Type> iCustomAutomationConfigurationEntityCollections = new Dictionary<string, Type>();
        //private Type iCustomAutomationConfigurationEntityCollection;
        private Type iCustomAutomationConfigurationStructureCollection;
        private Type iCustomAutomationConfigurationValueCollection;

        private string Tenant;

        /// <summary>
        /// Initializes a new instance of the <see cref="SMTManagementOrchestration"/> class.
        /// </summary>
        [Microsoft.Extensions.DependencyInjection.ActivatorUtilitiesConstructor]
        public CustomAutomationConfigurationOrchestration(
            IMaterialOrchestration materialOrchestration,
            IResourceOrchestration resourceOrchestration,
            IExceptionProtocol exceptionProtocol,
            IDispatchOrchestration dispatchOrchestration,
            IEntityTypeOrchestration entityTypeOrchestration,
            IConfigurationOrchestration configurationOrchestration,
            IGenericServiceOrchestration genericServiceOrchestration
            ) : base()
        {
            var services = ApplicationContext.CurrentServiceProvider;
            // Get Entity Factory
            _entityFactory = services.GetService<IEntityFactory>();

            _materialOrchestration = materialOrchestration;
            _resourceOrchestration = resourceOrchestration;
            _exceptionProtocol = exceptionProtocol;
            _dispatchOrchestration = dispatchOrchestration;
            _entityTypeOrchestration = entityTypeOrchestration;
            _configurationOrchestration = configurationOrchestration;
            _genericServiceOrchestration = genericServiceOrchestration;

            var utils = typeof(Utilities).GetFields(BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public | BindingFlags.Static);
            string tenant = utils.FirstOrDefault(k => k.Name.Contains("ClientTenantName")).GetValue(null)?.ToString();
            Tenant = tenant;
            Microsoft.Extensions.DependencyInjection.ServiceCollection sc = new Microsoft.Extensions.DependencyInjection.ServiceCollection();
            RegisterCustomEntityTypes(tenant, "CustomAutomationConfiguration", out iCustomAutomationConfiguration, out iCustomAutomationConfigurationCollection);
            //sc.AddTransient(typeof(ICustomAutomationConfiguration), iCustomAutomationConfiguration);
            //sc.AddTransient(typeof(ICustomAutomationConfigurationCollection), iCustomAutomationConfigurationCollection);
            //RegisterCustomEntityTypes(tenant, "CustomAutomationConfigurationEntity", out iCustomAutomationConfigurationEntity, out iCustomAutomationConfigurationEntityCollection);
            RegisterCustomEntityTypes(tenant, "CustomAutomationConfigurationStructure", out iCustomAutomationConfigurationStructure, out iCustomAutomationConfigurationStructureCollection);
            RegisterCustomEntityTypes(tenant, "CustomAutomationConfigurationValue", out iCustomAutomationConfigurationValue, out iCustomAutomationConfigurationValueCollection);

            var entityTypesWithConnectIoTEnable = _entityTypeOrchestration.GetAllEntityTypes(
            new GetAllEntityTypesInput()).EntityTypes.Where(et => et.ConnectIoTEnabled);
            Config.TryGetConfig(CustomAutomationConfigurationConstants.CustomEntityToExcludeFromConfigurationRelationConfiguration, out IConfig exclusion);

            List<string> entityNameExclude = new List<string>();
            if (exclusion != null && exclusion.Value != null && !String.IsNullOrEmpty(exclusion.Value.ToString()))
            {
                entityNameExclude.AddRange(exclusion.Value.ToString().Split(new string[] { ";" }, StringSplitOptions.RemoveEmptyEntries));
            }

            foreach (var entityType in entityTypesWithConnectIoTEnable)
            {
                if (entityNameExclude.Contains(entityType.Name))
                {
                    continue;
                }

                Type typeEntity;
                Type typeEntityCollection;
                RegisterCustomEntityTypes(tenant, $"CustomAutomationConfigurationEntity{entityType.Name}", out typeEntity, out typeEntityCollection);
                iCustomAutomationConfigurationEntities.Add(entityType.Name, typeEntity);
                iCustomAutomationConfigurationEntityCollections.Add(entityType.Name, typeEntityCollection);
            }
        }

        public CustomAutomationConfigurationOrchestration()
        {
        }

        private void RegisterCustomEntityTypes(string tenant, string entityName, out Type instanceInterface, out Type instanceInterfaceCollection)
        {
            string typeName = $"Cmf.Custom.{tenant}.BusinessObjects.Abstractions.I{entityName}";
            string collectionTypeName = $"{typeName}Collection";
            string assemblyPath = $"Cmf.Custom.{tenant}.BusinessObjects.{entityName}.dll";
            if (File.Exists(assemblyPath))
            {
                var customEntityAssembly = Assembly.LoadFrom(assemblyPath);
                instanceInterface = customEntityAssembly.GetType(typeName);
                instanceInterfaceCollection = customEntityAssembly.GetType(collectionTypeName);
            }
            else
            {
                instanceInterface = null;
                instanceInterfaceCollection = null;
            }
        }

        public CustomAutomationCreateConfigurationMetadataOutput CustomAutomationCreateConfigurationMetadata(CustomAutomationCreateConfigurationMetadataInput customAutomationCreateConfigurationMetadataInput)
        {
            Utilities.StartMethod(OBJECT_TYPE_NAME, "CustomAutomationCreateConfigurationMetadata",
          new KeyValuePair<string, object>(nameof(CustomAutomationCreateConfigurationMetadataInput),
              customAutomationCreateConfigurationMetadataInput));

            var automationProtocol = customAutomationCreateConfigurationMetadataInput.AutomationProtocol;
            var automationProtocolName = automationProtocol.Name;

            var result = new CustomAutomationCreateConfigurationMetadataOutput()
            {
                AutomationProtocol = automationProtocol,
                ParameterCollection = _entityFactory.CreateCollection<IParameterCollection>()
            };

            Dictionary<IParameter, object> parameterDefaultCollection = new Dictionary<IParameter, object>();
            IParameterCollection parameterCollection = _entityFactory.CreateCollection<IParameterCollection>();
            foreach (var automationProtocolParameter in automationProtocol.Parameters)
            {
                string parameterName = $"ConfigurationAutomationParameter_{System.Globalization.CultureInfo.CurrentCulture.TextInfo.ToTitleCase(automationProtocolName).Replace(" ", "")}_{System.Globalization.CultureInfo.CurrentCulture.TextInfo.ToTitleCase(automationProtocolParameter.Name).Replace(" ", "")}";
                IParameter parameter = _entityFactory.Create<IParameter>();
                if (!parameter.ObjectExists(parameterName))
                {
                    parameter.Name = parameterName;
                    parameter.Type = "AutomationConfiguration";
                    switch (automationProtocolParameter.DataType)
                    {
                        case AutomationDataType.String:
                        case AutomationDataType.Binary:
                        case AutomationDataType.Object:
                        case AutomationDataType.Password:
                        case AutomationDataType.Text:
                        case AutomationDataType.Culture:
                        case AutomationDataType.ExtensionParameters:
                            parameter.DataType = ParameterDataType.String;
                            break;
                        case AutomationDataType.Date:
                        case AutomationDataType.Time:
                        case AutomationDataType.DateTime:
                            parameter.DataType = ParameterDataType.DateTime;
                            break;
                        case AutomationDataType.Decimal:
                            parameter.DataType = ParameterDataType.Decimal;
                            break;
                        case AutomationDataType.Integer:
                        case AutomationDataType.Long:
                            parameter.DataType = ParameterDataType.Long;
                            break;
                        case AutomationDataType.Boolean:
                            parameter.DataType = ParameterDataType.Boolean;
                            break;
                    }
                    parameter.DisplayName = automationProtocolParameter.Name;
                    parameter.ParameterScope = ParameterScope.EDC_SPC_Recipe;

                    parameterCollection.Add(parameter);
                    parameterDefaultCollection.Add(parameter, automationProtocolParameter.DefaultValue);
                }


            }
            if (parameterCollection.Count > 0)
            {
                parameterCollection.Create();
            }

            if (parameterDefaultCollection.Count > 0)
            {
                var smartTable = _entityFactory.Create<ISmartTable>();
                smartTable.Load("CustomAutomationConfigurationMetadata");
                System.Data.DataSet dataSet = (smartTable as SmartTable).GetEmptyTableDataSet();

                foreach (var parameterDefault in parameterDefaultCollection)
                {
                    DataRow row = dataSet.Tables[0].NewRow();
                    row["ConfigurationLayer"] = "DriverInstance";
                    row["AutomationProtocol"] = automationProtocol.Name;
                    row["Parameter"] = parameterDefault.Key.Name;
                    row["DefaultValue"] = parameterDefault.Value;

                    dataSet.Tables[0].Rows.Add(row);
                }
                smartTable.InsertOrUpdateRows(NgpDataSet.FromDataSet(dataSet));
            }


            Utilities.EndMethod(-1, -1,
             new KeyValuePair<string, object>(nameof(CustomAutomationCreateConfigurationMetadataInput), customAutomationCreateConfigurationMetadataInput),
             new KeyValuePair<string, object>(nameof(CustomAutomationCreateConfigurationMetadataOutput), result));
            return result;
        }

        public CustomAutomationCreateConfigurationEntitiesOutput CustomAutomationCreateConfigurationEntities(CustomAutomationCreateConfigurationEntitiesInput customAutomationCreateConfigurationEntitiesInput)
        {
            Utilities.StartMethod(OBJECT_TYPE_NAME, "CustomAutomationCreateConfigurationEntities",
             new KeyValuePair<string, object>(nameof(CustomAutomationCreateConfigurationEntitiesInput),
             customAutomationCreateConfigurationEntitiesInput));

            IAutomationControllerInstance automationControllerInstance = customAutomationCreateConfigurationEntitiesInput.AutomationControllerInstance;
            IAutomationDriverInstanceCollection automationDriverInstanceCollection = customAutomationCreateConfigurationEntitiesInput.AutomationDriverInstanceCollection;
            dynamic parentController = _entityFactory.Create(iCustomAutomationConfiguration);
            dynamic childConfigurations = _entityFactory.CreateCollection(iCustomAutomationConfigurationCollection);
            dynamic configurationList = _entityFactory.CreateCollection(iCustomAutomationConfigurationCollection);

            Collection<object> entityCollection = new Collection<object>();
            entityCollection.Add(parentController);

            var result = new CustomAutomationCreateConfigurationEntitiesOutput()
            {
                AutomationControllerInstance = automationControllerInstance,
                AutomationDriverInstanceCollection = automationDriverInstanceCollection,
                CustomAutomationConfigurationCollection = configurationList
            };
            parentController.AutomationControllerInstance = automationControllerInstance;
            parentController.AutomationConfigurationLayer = "ControllerInstance";
            var automationControler = automationControllerInstance.AutomationControllerVersion;

            automationControler.Load();

            SmartTable automationConfiguration = new SmartTable() { Name = "CustomAutomationConfigurationMetadata" };
            automationConfiguration.Load();

            foreach (var automationDriverInstance in automationDriverInstanceCollection)
            {

                dynamic childConfiguration = _entityFactory.Create(iCustomAutomationConfiguration);
                childConfiguration.AutomationControllerInstance = automationControllerInstance;
                childConfiguration.AutomationDriverInstance = automationDriverInstance;
                childConfiguration.AutomationConfigurationLayer = "DriverInstance";
                childConfigurations.Add(childConfiguration);
                entityCollection.Add(childConfiguration);
            }
            childConfigurations.Create();
            parentController.Create();

            dynamic structureRelation = _entityFactory.CreateCollection(iCustomAutomationConfigurationStructureCollection);
            foreach (var childConfiguration in childConfigurations)
            {
                dynamic child = _entityFactory.Create(iCustomAutomationConfigurationStructure);
                child.SourceEntity = parentController;
                child.TargetEntity = childConfiguration;
                structureRelation.Add(child);

                var driverDefinition = childConfiguration.AutomationDriverInstance.AutomationControllerDriverDefinition.AutomationDriverDefinition;
                driverDefinition.Load();
                var protocol = driverDefinition.AutomationProtocol;
                protocol.Load();
                //retrieve configurations for controller
                INgpDataRow resolve = new NgpDataRow()
                {
                      { "ConfigurationLayer", "DriverInstance" },
                      { "AutomationDriverDefinition",  driverDefinition.Name },
                      { "AutomationProtocol",  protocol.Name },
                };

                var data = automationConfiguration.Resolve(resolve, false);
                var ds = NgpDataSet.ToDataSet(data);

                dynamic configs = _entityFactory.CreateCollection(iCustomAutomationConfigurationValueCollection);
                if (ds != null && ds.Tables.Count > 0 && ds.Tables[0].Rows.Count > 0)
                {
                    foreach (DataRow configuration in ds.Tables[0].Rows)
                    {
                        dynamic configRelation = _entityFactory.Create(iCustomAutomationConfigurationValue);
                        configRelation.SourceEntity = childConfiguration;
                        IParameter parameter = _entityFactory.Create<IParameter>();
                        parameter.Load(configuration["Parameter"] as string);
                        configRelation.TargetEntity = parameter;
                        configRelation.Description = parameter.DisplayName;
                        configRelation.Value = configuration["DefaultValue"] as string;
                        configRelation.IsConfiguration = (configuration["IsConfiguration"] as bool?).HasValue ? (configuration["IsConfiguration"] as bool?).Value : false;
                        configs.Add(configRelation);
                    }
                }

                childConfiguration.AddRelations(configs);


                Type childRelatedEntityType = childConfiguration.AutomationDriverInstance.ObjectType.EntityTypeInterface;
                dynamic childConfigurationRelatedEntity = _entityFactory.Create(childRelatedEntityType);
                childConfigurationRelatedEntity.Load((childConfiguration.AutomationDriverInstance.ObjectId as long?).Value);
                if (!iCustomAutomationConfigurationEntities.ContainsKey(childConfigurationRelatedEntity.EntityType.Name))
                {
                    Type typeEntity;
                    Type typeEntityCollection;
                    RegisterCustomEntityTypes(Tenant, $"CustomAutomationConfigurationEntity{childConfigurationRelatedEntity.EntityType.Name}", out typeEntity, out typeEntityCollection);
                    iCustomAutomationConfigurationEntities.Add(childConfigurationRelatedEntity.EntityType.Name, typeEntity);
                    iCustomAutomationConfigurationEntityCollections.Add(childConfigurationRelatedEntity.EntityType.Name, typeEntityCollection);
                }
                dynamic relatedEntity = _entityFactory.Create(iCustomAutomationConfigurationEntities[childConfigurationRelatedEntity.EntityType.Name]);
                relatedEntity.SourceEntity = childConfigurationRelatedEntity;
                //childConfigurationRelatedEntity;
                relatedEntity.TargetEntity = childConfiguration;
                dynamic childConfigurationRelatedEntityCollection = _entityFactory.CreateCollection(iCustomAutomationConfigurationEntityCollections[childConfigurationRelatedEntity.EntityType.Name]);
                childConfigurationRelatedEntityCollection.Add(relatedEntity);
                childConfiguration.AddRelations(childConfigurationRelatedEntityCollection);

            }
            parentController.AddRelations(structureRelation);

            //retrieve configurations for controller
            INgpDataRow resolveKeys = new NgpDataRow()
            {
                    { "ConfigurationLayer", "ControllerInstance" },
                    { "AutomationControler",  automationControler.Name },
            };
            var resolvedData = automationConfiguration.Resolve(resolveKeys, false);
            var dataset = NgpDataSet.ToDataSet(resolvedData);

            dynamic configRelations = _entityFactory.CreateCollection(iCustomAutomationConfigurationValueCollection);
            if (dataset != null && dataset.Tables.Count > 0 && dataset.Tables[0].Rows.Count > 0)
            {
                foreach (DataRow configuration in dataset.Tables[0].Rows)
                {
                    dynamic configRelation = _entityFactory.Create(iCustomAutomationConfigurationValue);
                    configRelation.SourceEntity = parentController;
                    IParameter parameter = _entityFactory.Create<IParameter>();
                    parameter.Load(configuration["Parameter"] as string);
                    configRelation.TargetEntity = parameter;
                    configRelation.Description = parameter.DisplayName;
                    configRelation.Value = configuration["DefaultValue"] as string;
                    configRelations.Add(configRelation);
                }
            }
            parentController.AddRelations(configRelations);

            Type parentRelatedEntityType = parentController.AutomationControllerInstance.ObjectType.EntityTypeInterface;
            dynamic parentConfigurationRelatedEntity = _entityFactory.Create(parentRelatedEntityType);

            parentConfigurationRelatedEntity.Load((parentController.AutomationControllerInstance.ObjectId as long?).Value);
            dynamic relatedParentEntity = _entityFactory.Create(iCustomAutomationConfigurationEntities[parentConfigurationRelatedEntity.EntityType.Name]);

            relatedParentEntity.SourceEntity = parentConfigurationRelatedEntity;
            relatedParentEntity.TargetEntity = parentController;
            dynamic parentConfigurationRelatedEntityCollection = _entityFactory.CreateCollection(iCustomAutomationConfigurationEntityCollections[parentConfigurationRelatedEntity.EntityType.Name]);
            parentConfigurationRelatedEntityCollection.Add(relatedParentEntity);
            parentController.AddRelations(parentConfigurationRelatedEntityCollection);


            configurationList.Add(parentController);
            configurationList.AddRange(childConfigurations);

            ApplicationContext.CallContext.SetInformationContext("CreatedAutomationConfigurationEntities", entityCollection);

            result.FeedbackMessages = new Collection<FeedbackMessage>()
            {
                new FeedbackMessage
                {
                    InnerMessage = "Please configure the communication setting by using the following entities.",
                    MessageType = FeedbackMessageType.ObjectsToOpen,
                    Objects = entityCollection
                }
            };


            Utilities.EndMethod(-1, -1,
            new KeyValuePair<string, object>(nameof(CustomAutomationCreateConfigurationEntitiesInput), customAutomationCreateConfigurationEntitiesInput),
            new KeyValuePair<string, object>(nameof(CustomAutomationCreateConfigurationEntitiesOutput), result));
            return result;
        }

        public CustomAutomationRetrieveConfigurationOutput CustomAutomationRetrieveConfiguration(CustomAutomationRetrieveConfigurationInput customAutomationRetriveConfigurationInput)
        {
            Utilities.StartMethod(OBJECT_TYPE_NAME, "CustomAutomationRetrieveConfiguration",
              new KeyValuePair<string, object>(nameof(CustomAutomationRetrieveConfigurationInput),
              customAutomationRetriveConfigurationInput));

            var automationConfigurationData = new AutomationConfigurationData();
            var result = new CustomAutomationRetrieveConfigurationOutput()
            {
                RelatedEntityName = customAutomationRetriveConfigurationInput.RelatedEntityName,
                RelatedEntityTypeName = customAutomationRetriveConfigurationInput.RelatedEntityTypeName,
                AutomationConfigurationData = automationConfigurationData
            };

            Type relatedEntityType = _entityTypeOrchestration.GetEntityTypeByName(new GetEntityTypeByNameInput()
            { Name = customAutomationRetriveConfigurationInput.RelatedEntityTypeName }).EntityType.EntityTypeInterface;

            dynamic relatedEntity = _entityFactory.Create(relatedEntityType);
            relatedEntity.Load(customAutomationRetriveConfigurationInput.RelatedEntityName);
            relatedEntity.LoadRelations();

            dynamic customAutomationConfiguration = null;
            var relationCollection = ((CmfEntityRelationCollection)relatedEntity.RelationCollection).Where(e => e.Key.StartsWith(CustomAutomationConfigurationConstants.CustomEntityTypeCustomAutomationConfigurationEntity)).First().Value;
            if (relationCollection.Count == 1)
            {
                customAutomationConfiguration = relationCollection.First().TargetEntity;
            }
            else
            {
                foreach (var relation in relationCollection)
                {
                    dynamic target = relation.TargetEntity;
                    if (customAutomationRetriveConfigurationInput.RetrieveControllerConfiguration.Value && target.AutomationConfigurationLayer.Equals("ControllerInstance"))
                    {
                        customAutomationConfiguration = target;
                        break;
                    }
                    else if (!customAutomationRetriveConfigurationInput.RetrieveControllerConfiguration.Value && target.AutomationConfigurationLayer.Equals("DriverInstance"))
                    {
                        customAutomationConfiguration = target;
                        break;
                    }
                }

            }
            if (customAutomationConfiguration == null)
            {
                throw new CmfBaseException("There is no Automation Configuration for the given entity");
            }
            customAutomationConfiguration.Load();
            if (customAutomationConfiguration.AutomationConfigurationLayer.Equals("DriverInstance"))
            {
                AutomationDriverInstance adi = customAutomationConfiguration.AutomationDriverInstance;
                adi.Load();
                adi.AutomationControllerDriverDefinition.Load();
                automationConfigurationData.AutomationConfigurationDriverFriendlyName = adi.AutomationControllerDriverDefinition.Name;
            }
            automationConfigurationData.AutomationConfigurationName = customAutomationConfiguration.Name;
            automationConfigurationData.AutomationConfigurationValues = new List<AutomationConfigurationValue>();

            customAutomationConfiguration.LoadRelations();
            automationConfigurationData.AutomationConfigurationRelatedEntityName = ((CmfEntityRelationCollection)customAutomationConfiguration.RelationCollection).First(e => e.Key.StartsWith(CustomAutomationConfigurationConstants.CustomEntityTypeCustomAutomationConfigurationEntity)).Value.First().SourceEntity.Name;


            if (((CmfEntityRelationCollection)customAutomationConfiguration.RelationCollection).ContainsKey(CustomAutomationConfigurationConstants.CustomEntityTypeCustomAutomationConfigurationValue))
            {
                foreach (var relation in customAutomationConfiguration.RelationCollection[CustomAutomationConfigurationConstants.CustomEntityTypeCustomAutomationConfigurationValue])
                {
                    var value = relation.Value;
                    IParameter parameter = relation.TargetEntity;
                    parameter.Load();
                    if ((relation.IsConfiguration as bool?).HasValue ? relation.IsConfiguration : false)
                    {
                        Config.TryGetConfig(value, out IConfig config);
                        value = config.Value.ToString();
                    }

                    automationConfigurationData.AutomationConfigurationValues.Add(new AutomationConfigurationValue()
                    {
                        Value = value,
                        Name = parameter.DisplayName
                    });
                }
            }

            Utilities.EndMethod(-1, -1,
                new KeyValuePair<string, object>(nameof(CustomAutomationRetrieveConfigurationInput), customAutomationRetriveConfigurationInput),
                new KeyValuePair<string, object>(nameof(CustomAutomationRetrieveConfigurationOutput), result));
            return result;
        }

        public CustomAutomationCreateRelatedEntityRelationOnConnectIoTEnabledOutput CustomAutomationCreateRelatedEntityRelationOnConnectIoTEnabled(CustomAutomationCreateRelatedEntityRelationOnConnectIoTEnabledInput customAutomationCreateRelatedEntityRelationOnConnectIoTEnabledInput)
        {
            Utilities.StartMethod(OBJECT_TYPE_NAME, "CustomAutomationCreateRelatedEntityRelationOnConnectIoTEnabled",
        new KeyValuePair<string, object>(nameof(CustomAutomationCreateRelatedEntityRelationOnConnectIoTEnabledInput),
        customAutomationCreateRelatedEntityRelationOnConnectIoTEnabledInput));

            CustomAutomationCreateRelatedEntityRelationOnConnectIoTEnabledOutput result = new CustomAutomationCreateRelatedEntityRelationOnConnectIoTEnabledOutput()
            {
                EntityType = customAutomationCreateRelatedEntityRelationOnConnectIoTEnabledInput.EntityType,
                RelatedEntityTypeCreated = null
            };

            var entityTypeToCreateRelation = customAutomationCreateRelatedEntityRelationOnConnectIoTEnabledInput.EntityType;
            if (entityTypeToCreateRelation.ConnectIoTEnabled)
            {

                // This will look for any entity type that has the Connect IoT flag enabled and create a relation between the configuration
                // and that entity types
                // Entity type with name contained on the Configuration /Cmf/Custom/Automation/AutomationConfigurationAutomation/EntityToExcludeFromConfigurationRelation
                // will not have a relation created

                //var entityTypesWithConnectIoTEnable = _entityTypeOrchestration.GetAllEntityTypes(
                //    new GetAllEntityTypesInput()).EntityTypes.Where(et => et.ConnectIoTEnabled);

                List<string> entityNameExclude = new List<string>();

                var configExist = _configurationOrchestration.ConfigExists(new ConfigExistsInput()
                {
                    Path = CustomAutomationConfigurationConstants.CustomEntityToExcludeFromConfigurationRelationConfiguration
                });
                if (configExist.ConfigExists)
                {
                    var getConfig = _configurationOrchestration.GetConfigByPath(new GetConfigByPathInput()
                    {
                        Path = CustomAutomationConfigurationConstants.CustomEntityToExcludeFromConfigurationRelationConfiguration
                    });

                    if (getConfig.Config != null)
                    {
                        entityNameExclude.AddRange(getConfig.Config.Value.ToString().Split(new string[] { ";" }, StringSplitOptions.RemoveEmptyEntries));
                    }
                }

                //relation should not be create for entities that are excluded
                if (!entityNameExclude.Contains(entityTypeToCreateRelation.Name))
                {

                    // Only makes sense to proceed if entity type doesn't exist yet
                    IEntityType relatedEntityRelationEntityType = _entityTypeOrchestration.GetAllEntityTypes(
                        new GetAllEntityTypesInput()).EntityTypes.FirstOrDefault(
                        e => e.Name.Equals($"{CustomAutomationConfigurationConstants.CustomEntityTypeCustomAutomationConfigurationEntity}{entityTypeToCreateRelation.Name}",
                        StringComparison.InvariantCultureIgnoreCase));

                    if (relatedEntityRelationEntityType == null)
                    {
                        #region Create Entity Type

                        relatedEntityRelationEntityType = new EntityType
                        {
                            Name = $"{CustomAutomationConfigurationConstants.CustomEntityTypeCustomAutomationConfigurationEntity}{entityTypeToCreateRelation.Name}",
                            Description = $"Custom Relation between {entityTypeToCreateRelation.Name} and {CustomAutomationConfigurationConstants.CustomEntityTypeCustomAutomationConfiguration}",
                            IsRelation = true,
                            SourceRelationEntityTypeId = entityTypeToCreateRelation.Id,
                            TargetRelationEntityTypeId = _entityTypeOrchestration.GetEntityTypeByName(new GetEntityTypeByNameInput
                            { Name = CustomAutomationConfigurationConstants.CustomEntityTypeCustomAutomationConfiguration }).EntityType.Id,
                            HistoryRetentionTime = 180,
                            IsHistoryEnabled = true,
                            IsUniqueNameRequired = true,
                            ReplicateToODS = true,
                            AllowAttributes = true,
                            AllowDeleteInstances = false,
                            IsVisible = true,
                            Icon = "icon-docs-st-lg-installation"
                        };

                        _entityTypeOrchestration.CreateEntityType(new CreateEntityTypeInput
                        {
                            EntityType = relatedEntityRelationEntityType
                        });
                        result.RelatedEntityTypeCreated = relatedEntityRelationEntityType;
                        #endregion

                        #region Generate Schema

                        _entityTypeOrchestration.GenerateEntityTypeDBSchema(new GenerateEntityTypeDBSchemaInput
                        {
                            EntityType = relatedEntityRelationEntityType
                        });

                        //register entity types
                        Type typeEntity;
                        Type typeEntityCollection;
                        RegisterCustomEntityTypes(Tenant, $"CustomAutomationConfigurationEntity{relatedEntityRelationEntityType.Name}", out typeEntity, out typeEntityCollection);
                        iCustomAutomationConfigurationEntities.Add(relatedEntityRelationEntityType.Name, typeEntity);
                        iCustomAutomationConfigurationEntityCollections.Add(relatedEntityRelationEntityType.Name, typeEntityCollection);
                    }
                    #endregion


                }
            }
            Utilities.EndMethod(-1, -1,
              new KeyValuePair<string, object>(nameof(CustomAutomationCreateRelatedEntityRelationOnConnectIoTEnabledInput), customAutomationCreateRelatedEntityRelationOnConnectIoTEnabledInput),
              new KeyValuePair<string, object>(nameof(CustomAutomationCreateRelatedEntityRelationOnConnectIoTEnabledOutput), result));
            return result;

        }

        public CustomAutomationUpdateConfigurationEntitiesOutput CustomAutomationUpdateConfigurationEntities(CustomAutomationUpdateConfigurationEntitiesInput customAutomationUpdateConfigurationEntitiesInput)
        {
            Utilities.StartMethod(OBJECT_TYPE_NAME, "CustomAutomationUpdateConfigurationEntities",
            new KeyValuePair<string, object>(nameof(CustomAutomationUpdateConfigurationEntitiesInput),
            customAutomationUpdateConfigurationEntitiesInput));

            if (customAutomationUpdateConfigurationEntitiesInput.AutomationConfigurationsToUpdate.IsNullOrEmpty())
            {
                throw new Exception("CustomAutomationUpdateConfigurationEntities - Input property AutomationConfigurationsToUpdate is null or empty.");
            }

            CustomAutomationUpdateConfigurationEntitiesOutput result = new CustomAutomationUpdateConfigurationEntitiesOutput();

            if (customAutomationUpdateConfigurationEntitiesInput.ActionGroupName == "ConnectIoTManagement.ConnectIoTManagementOrchestration.FullUpdateAutomationControllerInstance.Pre")
            {
                Type customAutomationConfigurationType = _entityTypeOrchestration.GetEntityTypeByName(new GetEntityTypeByNameInput()
                { Name = "CustomAutomationConfiguration" }).EntityType.EntityTypeInterface;

                //List<string> automationConfigurationsToUpdate = new List<string>();

                foreach (string configurationName in customAutomationUpdateConfigurationEntitiesInput.AutomationConfigurationsToUpdate)
                {
                    dynamic customAutomationConfiguration = _entityFactory.Create(customAutomationConfigurationType);
                    customAutomationConfiguration.Load(configurationName);
                    customAutomationConfiguration.AutomationDriverInstance = null;
                    customAutomationConfiguration.Save();
                }
            }
            else if (customAutomationUpdateConfigurationEntitiesInput.ActionGroupName == "ConnectIoTManagement.ConnectIoTManagementOrchestration.FullUpdateAutomationControllerInstance.Post")
            {
                Type customAutomationConfigurationType = _entityTypeOrchestration.GetEntityTypeByName(new GetEntityTypeByNameInput()
                { Name = "CustomAutomationConfiguration" }).EntityType.EntityTypeInterface;

                foreach (string configurationName in customAutomationUpdateConfigurationEntitiesInput.AutomationConfigurationsToUpdate)
                {
                    dynamic customAutomationConfiguration = _entityFactory.Create(customAutomationConfigurationType);
                    customAutomationConfiguration.Load(configurationName);

                    customAutomationConfiguration.AutomationDriverInstance = customAutomationUpdateConfigurationEntitiesInput.UpdatedDriverInstance;
                    customAutomationConfiguration.Save();
                }
            }
            else
            {
                throw new Exception("Action group is invalid, expecting ConnectIoTManagement.ConnectIoTManagementOrchestration.FullUpdateAutomationControllerInstance.Pre/Post.");
            }

            Utilities.EndMethod(-1, -1,
            new KeyValuePair<string, object>(nameof(CustomAutomationUpdateConfigurationEntitiesInput), customAutomationUpdateConfigurationEntitiesInput),
            new KeyValuePair<string, object>(nameof(CustomAutomationUpdateConfigurationEntitiesOutput), result));
            return result;
        }
    }
}
