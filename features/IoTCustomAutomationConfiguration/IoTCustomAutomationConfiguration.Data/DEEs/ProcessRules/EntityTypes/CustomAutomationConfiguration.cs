using System;
using System.Collections.Generic;
using Cmf.Custom.SemiconductorTemplate.Common;
using Cmf.Foundation.BusinessObjects;
using Cmf.Foundation.BusinessOrchestration.EntityTypeManagement;
using Cmf.Foundation.BusinessOrchestration.EntityTypeManagement.InputObjects;
using Cmf.Foundation.Common;
using Cmf.Foundation.Common.Base;
using System.Linq;
using Cmf.Foundation.BusinessObjects.Abstractions;
using Cmf.Foundation.BusinessOrchestration.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Cmf.Foundation.BusinessOrchestration.TableManagement;
using Cmf.Foundation.BusinessOrchestration.TableManagement.InputObjects;
using static Stimulsoft.Report.StiOptions;
using System.Reflection;
using Cmf.Community.IoTCustomAutomationConfiguration.Actions.Common;
using System.Drawing;
using Cmf.Foundation.BusinessOrchestration.ConfigurationManagement.InputObjects;

namespace Cmf.Community.IoTCustomAutomationConfiguration.Actions.Actions.ProcessRules.EntityTypes
{
    class CustomAutomationConfiguration : DeeDevBase
    {
        public override bool DeeTestCondition(Dictionary<string, object> Input)
        {
            //---Start DEE Condition Code---

            /* Description:
             *     DEE Action to create a CustomMaterialMovement Entity
            */

            return true;

            //---End DEE Condition Code---
        }

        public override Dictionary<string, object> DeeActionCode(Dictionary<string, object> Input)
        {
            //---Start DEE Code---

            // Foundation
            UseReference("", "Cmf.Foundation.BusinessOrchestration.Abstractions");
            UseReference("", "Cmf.Foundation.BusinessOrchestration.EntityTypeManagement.InputObjects");
            UseReference("", "Cmf.Foundation.BusinessOrchestration.TableManagement.InputObjects");
            UseReference("", "Cmf.Foundation.BusinessOrchestration.ConfigurationManagement.InputObjects");
            // Custom
            UseReference("Cmf.Community.IoTCustomAutomationConfiguration.Actions.Common.dll", "Cmf.Community.IoTCustomAutomationConfiguration.Actions.Common");

            // Name of the entity to be generated
            string newEntityName = CustomAutomationConfigurationConstants.CustomEntityTypeCustomAutomationConfiguration;
            string propertyAutomationDriverInstance = "AutomationDriverInstance";
            string propertyAutomationControllerInstance = Foundation.Common.Constants.AutomationControllerInstance;
            string propertyAutomationConfigurationLayer = "AutomationConfigurationLayer";

            // Get services provider information
            IServiceProvider serviceProvider = (IServiceProvider)Input["ServiceProvider"];
            IEntityTypeOrchestration entityTypeOrchestration = serviceProvider.GetService<IEntityTypeOrchestration>();
            ITableOrchestration tableOrchestration = serviceProvider.GetRequiredService<ITableOrchestration>();
            IConfigurationOrchestration configurationOrchestration = serviceProvider.GetRequiredService<IConfigurationOrchestration>();
            #region Main entity
            //Only makes sense to proceed if entity type doesn't exist yet
            IEntityType foundEntity = entityTypeOrchestration.GetAllEntityTypes(
                new GetAllEntityTypesInput()).EntityTypes.FirstOrDefault(E => string.Equals(E.Name, newEntityName, StringComparison.InvariantCultureIgnoreCase));
            if (foundEntity == null)
            {
                #region Create Entity Type
                // set new entity type to be created
                foundEntity = new EntityType()
                {
                    Name = newEntityName,
                    Description = CustomAutomationConfigurationConstants.CustomEntityTypeCustomAutomationConfigurationDescription,
                    IsRelation = false,
                    IsUniqueNameRequired = true,
                    ReplicateToODS = true,
                    IsHistoryEnabled = true,
                    IsVisible = true,
                    AllowAttributes = true,
                    AllowOperationAttributes = true,
                    HistoryRetentionTime = 30,
                    HistoryDefaultInterval = 180,
                    IsCloneable = true,
                    Icon = "icon-mes-nav-manufacturing",
                    LocalizedMessageKey = "Cmf.Custom.CustomAutomationConfiguration.BusinessObjects.CustomAutomationConfiguration"
                };
                foundEntity = entityTypeOrchestration.CreateEntityType(new CreateEntityTypeInput() { EntityType = foundEntity }).EntityType;
                #endregion
            }
            #region Add properties
            // if entity is still in Created state, check if all necessary properties are already added
            if (foundEntity.UniversalState == UniversalState.Created)
            {
                // check if required properties 
                IScalarType bigIntScalarType = new ScalarType();
                bigIntScalarType.Load("BigInt");
                IScalarType stringScalarType = new ScalarType();
                stringScalarType.Load("NVarChar");

                #region Entity Type Properties
                IEntityTypePropertyCollection propertiesToAdd = new EntityTypePropertyCollection();
                // Automation Driver Instance Reference
                if (!foundEntity.Properties.Any(E => String.Equals(E.Name, propertyAutomationDriverInstance, StringComparison.InvariantCultureIgnoreCase)))
                {
                    // Automation Driver Instance Reference
                    IEntityTypeProperty propLogisticalProcess = new EntityTypeProperty()
                    {
                        Name = propertyAutomationDriverInstance,
                        Description = "Automation Driver Instance connected to this configuration",
                        PropertyType = EntityTypePropertyType.Property,
                        ReferenceType = ReferenceType.EntityType,
                        ReferencedObjectId = entityTypeOrchestration.GetEntityTypeByName(new GetEntityTypeByNameInput
                        { Name = "AutomationDriverInstance" }).EntityType.Id,
                        ScalarType = bigIntScalarType,
                        IsEnabled = true,
                        IsIndexed = false,
                        IsHistoryEnable = true,
                        LoadToDWH = true
                    };
                    // add property to list of properties to add...
                    propertiesToAdd.Add(propLogisticalProcess);
                }

                // Automation Controller Instance reference
                if (!foundEntity.Properties.Any(E => String.Equals(E.Name, propertyAutomationDriverInstance, StringComparison.InvariantCultureIgnoreCase)))
                {
                    // Automation Controller Instance reference
                    IEntityTypeProperty propLogisticalProcess = new EntityTypeProperty()
                    {
                        Name = propertyAutomationControllerInstance,
                        Description = "Automation Controller Instance connected to this configuration",
                        PropertyType = EntityTypePropertyType.Property,
                        ReferenceType = ReferenceType.EntityType,
                        ReferencedObjectId = entityTypeOrchestration.GetEntityTypeByName(new GetEntityTypeByNameInput
                        { Name = Foundation.Common.Constants.AutomationControllerInstance }).EntityType.Id,
                        ScalarType = bigIntScalarType,
                        IsEnabled = true,
                        IsIndexed = false,
                        IsHistoryEnable = true,
                        LoadToDWH = true
                    };
                    // add property to list of properties to add...
                    propertiesToAdd.Add(propLogisticalProcess);
                }

                // Automation Controller Instance reference
                if (!foundEntity.Properties.Any(E => String.Equals(E.Name, propertyAutomationConfigurationLayer, StringComparison.InvariantCultureIgnoreCase)))
                {
                    ILookupTable lookupConfigurationType = new LookupTable();

                    // Load all lookup tables
                    ILookupTableCollection lookupTables = tableOrchestration.GetAllLookupTables(new GetAllLookupTablesInput()).LookupTables;


                    // Key = Name, Value = Description
                    Dictionary<string, string> lookupTablesToCreate = new Dictionary<string, string>
                {
                    { "CustomAutomationConfigurationMetadataConfigurationLayer", "Defines configuration layers for Automation Configuration" }
                };
                    foreach (KeyValuePair<string, string> lookupTableToCreate in lookupTablesToCreate)
                    {
                        ILookupTable lookupTable = lookupTables.FirstOrDefault(E => string.Equals(E.Name, lookupTableToCreate.Key));

                        // Check if table exists, if not creates the lookup table
                        if (lookupTable == null)
                        {
                            lookupTable = new LookupTable
                            {
                                Name = lookupTableToCreate.Key,
                                Description = lookupTableToCreate.Value
                            };

                            tableOrchestration.CreateLookupTable(new CreateLookupTableInput { LookupTable = lookupTable });
                        }
                        if (lookupTable.Name == "CustomAutomationConfigurationMetadataConfigurationLayer")
                        {
                            lookupConfigurationType = lookupTable;
                        }

                    }

                    // Automation Controller Instance reference
                    // isERPFinalConfirmation
                    IEntityTypeProperty propAutomationConfigurationLayer = new EntityTypeProperty()
                    {
                        Name = propertyAutomationConfigurationLayer,
                        Description = "Configuration Layer the Configuration applies to",
                        PropertyType = EntityTypePropertyType.Property,
                        IsEnabled = true,
                        ScalarType = stringScalarType,
                        ScalarSize = 256,
                        ReferenceType = ReferenceType.LookupValue,
                        ReferenceName = lookupConfigurationType.Name,
                        ReferencedObjectId = lookupConfigurationType.Id,
                        IsMandatory = true,
                        IsHistoryEnable = true
                    };
                    // add property to list of properties to add...
                    propertiesToAdd.Add(propAutomationConfigurationLayer);
                }

                #endregion
                // Add properties to entity
                if (propertiesToAdd.Count > 0)
                {
                    foundEntity = entityTypeOrchestration.AddEntityTypeProperties(new AddEntityTypePropertiesInput()
                    {
                        EntityType = foundEntity,
                        EntityTypeProperties = propertiesToAdd
                    }).EntityType;
                    bool isToUpdateEntity = false;
                    FullUpdateEntityTypeInput input = new FullUpdateEntityTypeInput()
                    {
                        EntityType = foundEntity,
                        EntityTypePropertiesToAddOrUpdate = new EntityTypePropertyCollection()
                    };
                    if (isToUpdateEntity)
                    {
                        entityTypeOrchestration.FullUpdateEntityType(input);
                    }
                }
                #region Generate Schema
                entityTypeOrchestration.GenerateEntityTypeDBSchema(new GenerateEntityTypeDBSchemaInput
                {
                    EntityType = foundEntity
                });
                #endregion
            }
            #endregion

            #endregion Main Entity

            #region Configuration Value Relation
            // Only makes sense to proceed if entity type doesn't exist yet
            IEntityType configurationValueEntityType = entityTypeOrchestration.GetAllEntityTypes(
                new GetAllEntityTypesInput()).EntityTypes.FirstOrDefault(
                e => e.Name.Equals(CustomAutomationConfigurationConstants.CustomEntityTypeCustomAutomationConfigurationValue,
                StringComparison.InvariantCultureIgnoreCase));

            if (configurationValueEntityType == null)
            {
                #region Create Entity Type

                configurationValueEntityType = new EntityType
                {
                    Name = CustomAutomationConfigurationConstants.CustomEntityTypeCustomAutomationConfigurationValue,
                    Description = CustomAutomationConfigurationConstants.CustomEntityTypeCustomAutomationConfigurationValueDescription,
                    IsRelation = true,
                    SourceRelationEntityTypeId = entityTypeOrchestration.GetEntityTypeByName(new GetEntityTypeByNameInput
                    {
                        Name = CustomAutomationConfigurationConstants.CustomEntityTypeCustomAutomationConfiguration
                    }).EntityType.Id,
                    TargetRelationEntityTypeId = entityTypeOrchestration.GetEntityTypeByName(new GetEntityTypeByNameInput
                    { Name = Navigo.Common.Constants.Parameter }).EntityType.Id,
                    HistoryRetentionTime = 180,
                    IsHistoryEnabled = true,
                    IsUniqueNameRequired = true,
                    ReplicateToODS = true,
                    AllowAttributes = true,
                    AllowDeleteInstances = false,
                    IsVisible = true,
                    Icon = "icon-core-st-lg-settings"
                };

                entityTypeOrchestration.CreateEntityType(new CreateEntityTypeInput
                {
                    EntityType = configurationValueEntityType
                });

                #endregion

                #region Add properties
                // if entity is still in Created state, check if all necessary properties are already added
                if (configurationValueEntityType.UniversalState == UniversalState.Created)
                {
                    // check if required properties 
                    IScalarType stringScalarType = new ScalarType();
                    stringScalarType.Load("NVarChar");
                    string propertyValue = "Value";
                    IScalarType bitScalarType = new ScalarType();
                    bitScalarType.Load("Bit");
                    string propertyConfig = "IsConfiguration";

                    #region Entity Type Properties
                    IEntityTypePropertyCollection propertiesToAdd = new EntityTypePropertyCollection();
                    // Property Value
                    if (!configurationValueEntityType.Properties.Any(E => String.Equals(E.Name, propertyValue, StringComparison.InvariantCultureIgnoreCase)))
                    {
                        // Property Value
                        IEntityTypeProperty propAutomationValue = new EntityTypeProperty()
                        {
                            Name = propertyValue,
                            Description = " Automation Configuration Value",
                            PropertyType = EntityTypePropertyType.Property,
                            ReferenceType = ReferenceType.None,
                            ScalarType = stringScalarType,
                            ScalarSize = 512,
                            IsEnabled = true,
                            IsIndexed = false,
                            IsHistoryEnable = true,
                            LoadToDWH = true
                        };
                        // add property to list of properties to add...
                        propertiesToAdd.Add(propAutomationValue);
                    }

                    // Property Value
                    if (!configurationValueEntityType.Properties.Any(E => String.Equals(E.Name, propertyConfig, StringComparison.InvariantCultureIgnoreCase)))
                    {
                        // Property Value
                        IEntityTypeProperty propAutomationValueIsConfiguration = new EntityTypeProperty()
                        {
                            Name = propertyConfig,
                            Description = " Automation Configuration References a Config by Path",
                            PropertyType = EntityTypePropertyType.Property,
                            ReferenceType = ReferenceType.None,
                            ScalarType = bitScalarType,
                            ScalarSize = 0,
                            IsEnabled = true,
                            IsIndexed = false,
                            IsHistoryEnable = true,
                            LoadToDWH = true
                        };
                        // add property to list of properties to add...
                        propertiesToAdd.Add(propAutomationValueIsConfiguration);
                    }

                    #endregion
                    // Add properties to entity
                    if (propertiesToAdd.Count > 0)
                    {
                        configurationValueEntityType = entityTypeOrchestration.AddEntityTypeProperties(new AddEntityTypePropertiesInput()
                        {
                            EntityType = configurationValueEntityType,
                            EntityTypeProperties = propertiesToAdd
                        }).EntityType;
                        bool isToUpdateEntity = false;
                        FullUpdateEntityTypeInput input = new FullUpdateEntityTypeInput()
                        {
                            EntityType = foundEntity,
                            EntityTypePropertiesToAddOrUpdate = new EntityTypePropertyCollection()
                        };
                        if (isToUpdateEntity)
                        {
                            entityTypeOrchestration.FullUpdateEntityType(input);
                        }
                    }
                    #region Generate Schema
                    entityTypeOrchestration.GenerateEntityTypeDBSchema(new GenerateEntityTypeDBSchemaInput
                    {
                        EntityType = configurationValueEntityType
                    });
                    #endregion
                }
                #endregion

                #region Generate Schema

                entityTypeOrchestration.GenerateEntityTypeDBSchema(new GenerateEntityTypeDBSchemaInput
                {
                    EntityType = configurationValueEntityType
                });

                #endregion
            }
            #endregion

            #region Configuration Related Relation
            // Only makes sense to proceed if entity type doesn't exist yet
            IEntityType configurationStructureEntityType = entityTypeOrchestration.GetAllEntityTypes(
                new GetAllEntityTypesInput()).EntityTypes.FirstOrDefault(
                e => e.Name.Equals(CustomAutomationConfigurationConstants.CustomEntityTypeCustomAutomationConfigurationStructure,
                StringComparison.InvariantCultureIgnoreCase));

            if (configurationStructureEntityType == null)
            {
                #region Create Entity Type

                configurationStructureEntityType = new EntityType
                {
                    Name = CustomAutomationConfigurationConstants.CustomEntityTypeCustomAutomationConfigurationStructure,
                    Description = CustomAutomationConfigurationConstants.CustomEntityTypeCustomAutomationConfigurationStructureDescription,
                    IsRelation = true,
                    SourceRelationEntityTypeId = entityTypeOrchestration.GetEntityTypeByName(new GetEntityTypeByNameInput
                    {
                        Name = CustomAutomationConfigurationConstants.CustomEntityTypeCustomAutomationConfiguration
                    }).EntityType.Id,
                    TargetRelationEntityTypeId = entityTypeOrchestration.GetEntityTypeByName(new GetEntityTypeByNameInput
                    { Name = CustomAutomationConfigurationConstants.CustomEntityTypeCustomAutomationConfiguration }).EntityType.Id,
                    HistoryRetentionTime = 180,
                    IsHistoryEnabled = true,
                    IsUniqueNameRequired = true,
                    ReplicateToODS = true,
                    AllowAttributes = true,
                    AllowDeleteInstances = false,
                    IsVisible = true,
                    Icon = "icon-core-st-lg-manage"
                };

                entityTypeOrchestration.CreateEntityType(new CreateEntityTypeInput
                {
                    EntityType = configurationStructureEntityType
                });

                #endregion

                #region Add properties

                #endregion

                #region Generate Schema

                entityTypeOrchestration.GenerateEntityTypeDBSchema(new GenerateEntityTypeDBSchemaInput
                {
                    EntityType = configurationStructureEntityType
                });

                #endregion
            }
            #endregion

            #region Relation to Entities
            // This will look for any entity type that has the Connect IoT flag enabled and create a relation between the configuration
            // and that entity types
            // Entity type with name contained on the Configuration /Cmf/Custom/Automation/AutomationConfigurationAutomation/EntityToExcludeFromConfigurationRelation
            // will not have a relation created

            var entityTypesWithConnectIoTEnable = entityTypeOrchestration.GetAllEntityTypes(
                new GetAllEntityTypesInput()).EntityTypes.Where(et => et.ConnectIoTEnabled);

            List<string> entityNameExclude = new List<string>();

            var configExist = configurationOrchestration.ConfigExists(new ConfigExistsInput()
            {
                Path = CustomAutomationConfigurationConstants.CustomEntityToExcludeFromConfigurationRelationConfiguration
            });
            if (configExist.ConfigExists)
            {
                var getConfig = configurationOrchestration.GetConfigByPath(new GetConfigByPathInput()
                {
                    Path = CustomAutomationConfigurationConstants.CustomEntityToExcludeFromConfigurationRelationConfiguration
                });

                if (getConfig.Config != null)
                {
                    entityNameExclude.AddRange(getConfig.Config.Value.ToString().Split(new string[] { ";" }, StringSplitOptions.RemoveEmptyEntries));
                }
            }

            foreach (IEntityType entityTypeToCreateRelation in entityTypesWithConnectIoTEnable)
            {
                //relation should not be create for entities that are excluded
                if (entityNameExclude.Contains(entityTypeToCreateRelation.Name))
                {
                    continue;
                }
                // Only makes sense to proceed if entity type doesn't exist yet
                IEntityType relatedEntityRelationEntityType = entityTypeOrchestration.GetAllEntityTypes(
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
                        SourceRelationEntityTypeId = entityTypeToCreateRelation.Id /*entityTypeOrchestration.GetEntityTypeByName(new GetEntityTypeByNameInput
                        {
                            Name = entityTypeToCreateRelation.Name
                        }).EntityType.Id*/,
                        TargetRelationEntityTypeId = entityTypeOrchestration.GetEntityTypeByName(new GetEntityTypeByNameInput
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

                    entityTypeOrchestration.CreateEntityType(new CreateEntityTypeInput
                    {
                        EntityType = relatedEntityRelationEntityType
                    });

                    #endregion

                    #region Generate Schema

                    entityTypeOrchestration.GenerateEntityTypeDBSchema(new GenerateEntityTypeDBSchemaInput
                    {
                        EntityType = relatedEntityRelationEntityType
                    });

                    #endregion
                }
            }
            #endregion
            //---End DEE Code---
            return Input;
        }
    }
}
