using Cmf.Common.CustomActionUtilities.Extensions;
using Cmf.Foundation.Common;
using Cmf.Foundation.Common.Abstractions;
using Cmf.Foundation.BusinessObjects.QueryObject;
using Cmf.Foundation.BusinessObjects.Abstractions;
using Cmf.Foundation.BusinessOrchestration.ConnectIoTManagement.InputObjects;
using Cmf.Foundation.BusinessOrchestration.ConnectIoTManagement.OutputObjects;
using System;
using System.Data;
using System.Collections.Generic;
using Microsoft.Extensions.DependencyInjection;
using Cmf.Community.IoTCustomAutomationConfiguration.Actions.Orchestration;
using Cmf.Community.IoTCustomAutomationConfiguration.Actions.Orchestration.InputObjects;

namespace Cmf.Community.IoTCustomAutomationConfiguration.Actions.Actions.Automation
{
    public class CustomAutomationUpdateConfigurationEntities : DeeDevBase
    {
        public override Dictionary<string, object> DeeActionCode(Dictionary<string, object> Input)
        {
            //---Start DEE Code---
            // System
            UseReference("", "System");
            UseReference("", "System.Data");
            UseReference("", "System.Collections.Generic");
            UseReference("", "Microsoft.Extensions.DependencyInjection");

            //Foundation
            UseReference("Cmf.Foundation.Common.dll", "Cmf.Foundation.Common");
            UseReference("Cmf.Foundation.Common.dll", "Cmf.Foundation.Common.Abstractions");
            UseReference("Cmf.Foundation.BusinessObjects.dll", "Cmf.Foundation.BusinessObjects.QueryObject");
            UseReference("Cmf.Foundation.BusinessObjects.dll", "Cmf.Foundation.BusinessObjects.Abstractions");
            UseReference("Cmf.Foundation.BusinessOrchestration.dll", "Cmf.Foundation.BusinessOrchestration.ConnectIoTManagement.InputObjects");
            UseReference("Cmf.Foundation.BusinessOrchestration.dll", "Cmf.Foundation.BusinessOrchestration.ConnectIoTManagement.OutputObjects");

            //Common
            UseReference("Cmf.Common.CustomActionUtilities.dll", "Cmf.Common.CustomActionUtilities.Extensions");

            //Custom
            UseReference("Cmf.Community.IoTCustomAutomationConfiguration.Actions.Orchestration.dll", "Cmf.Community.IoTCustomAutomationConfiguration.Actions.Orchestration");
            UseReference("Cmf.Community.IoTCustomAutomationConfiguration.Actions.Orchestration.dll", "Cmf.Community.IoTCustomAutomationConfiguration.Actions.Orchestration.InputObjects");


            IServiceProvider serviceProvider = (IServiceProvider)Input["ServiceProvider"];
            IEntityFactory entityFactory = serviceProvider.GetService<IEntityFactory>();
            ICustomAutomationConfigurationOrchestration configurationOrchestration = serviceProvider.GetService<ICustomAutomationConfigurationOrchestration>();

            if (Input["ActionGroupName"] as string == "ConnectIoTManagement.ConnectIoTManagementOrchestration.FullUpdateAutomationControllerInstance.Pre")
            {
                FullUpdateAutomationControllerInstanceInput fullUpdateAutomationControllerInstanceInput = Input["FullUpdateAutomationControllerInstanceInput"] as FullUpdateAutomationControllerInstanceInput;

                if (fullUpdateAutomationControllerInstanceInput.FullUpdateParameters is not null
                    && fullUpdateAutomationControllerInstanceInput.FullUpdateParameters.DriversToRemove is not null
                    && fullUpdateAutomationControllerInstanceInput.FullUpdateParameters.DriversToRemove.Count > 0)
                {
                    string automationDriverInstanceToRemove = fullUpdateAutomationControllerInstanceInput.FullUpdateParameters.DriversToRemove[0].Name;

                    //Get the AutomationConfigurations related to the updated AutomationDriverInstance
                    #region Query Automation Configurations
                    var query = new QueryObject
                    {
                        Description = string.Empty,
                        EntityTypeName = "CustomAutomationConfiguration",
                        Name = "AutomationConfigurationByAutomationControllerInstance",
                        Query = new Query
                        {
                            Distinct = false,
                            Filters = new FilterCollection{
                                new Cmf.Foundation.BusinessObjects.QueryObject.Filter
                                {
                                    Name = "Name",
                                    ObjectName = "AutomationDriverInstance",
                                    ObjectAlias = "CustomAutomationConfiguration_AutomationDriverInstance_2",
                                    Operator = Cmf.Foundation.Common.FieldOperator.IsEqualTo,
                                    Value = automationDriverInstanceToRemove,
                                    LogicalOperator = Cmf.Foundation.Common.LogicalOperator.Nothing,
                                    FilterType = Cmf.Foundation.BusinessObjects.QueryObject.Enums.FilterType.Normal,
                                }
                            },
                            Fields = new FieldCollection
                            {
                                new Field
                                {
                                    Alias = "Id",
                                    ObjectName = "CustomAutomationConfiguration",
                                    ObjectAlias = "CustomAutomationConfiguration_1",
                                    IsUserAttribute = false,
                                    Name = "Id",
                                    Position = 0,
                                    Sort = Cmf.Foundation.Common.FieldSort.NoSort
                                },
                                new Field
                                {
                                    Alias = "Name",
                                    ObjectName = "CustomAutomationConfiguration",
                                    ObjectAlias = "CustomAutomationConfiguration_1",
                                    IsUserAttribute = false,
                                    Name = "Name",
                                    Position = 1,
                                    Sort = Cmf.Foundation.Common.FieldSort.NoSort
                                }
                            },
                            Relations = new RelationCollection {
                                new Relation()
                                {
                                    Alias = "",
                                    IsRelation = false,
                                    Name = "",
                                    SourceEntity = "CustomAutomationConfiguration",
                                    SourceEntityAlias = "CustomAutomationConfiguration_1",
                                    SourceJoinType = Cmf.Foundation.BusinessObjects.QueryObject.Enums.JoinType.InnerJoin,
                                    SourceProperty = "AutomationDriverInstanceId",
                                    TargetEntity = "AutomationDriverInstance",
                                    TargetEntityAlias = "CustomAutomationConfiguration_AutomationDriverInstance_2",
                                    TargetJoinType = Cmf.Foundation.BusinessObjects.QueryObject.Enums.JoinType.InnerJoin,
                                    TargetProperty = "Id"
                                }
                            }
                        }
                    };
                    #endregion

                    DataSet automationConfigurationsDataSet = query.Execute(false, new QueryParameterCollection());

                    //Remove the AutomationConfigurations' AutomationDriverInstances to allow the AutomationControllerInstance to Update
                    if (automationConfigurationsDataSet.HasData())
                    {
                        List<string> automationConfigurationsToUpdate = new List<string>();

                        foreach (DataRow row in automationConfigurationsDataSet.Tables[0].Rows)
                        {
                            string configurationName = row["Name"] as string ?? "";
                            if (configurationName.Length > 0)
                            {
                                automationConfigurationsToUpdate.Add(configurationName);
                            }
                        }

                        if (automationConfigurationsToUpdate.Count > 0)
                        {
                            configurationOrchestration.CustomAutomationUpdateConfigurationEntities(new CustomAutomationUpdateConfigurationEntitiesInput()
                            {
                                AutomationConfigurationsToUpdate = automationConfigurationsToUpdate,
                                ActionGroupName = Input["ActionGroupName"] as string
                            });

                            ApplicationContext.CallContext.SetInformationContext("AutomationConfigurationsToUpdate", automationConfigurationsToUpdate);
                        }
                    }
                }
            }
            else if (Input.ContainsKey("FullUpdateAutomationControllerInstanceOutput") && Input["FullUpdateAutomationControllerInstanceOutput"] is not null)
            {
                var fullUpdateAutomationControllerInstanceOutput = Input["FullUpdateAutomationControllerInstanceOutput"] as FullUpdateAutomationControllerInstanceOutput;

                if (fullUpdateAutomationControllerInstanceOutput.AutomationControllerInstance is not null
                    && fullUpdateAutomationControllerInstanceOutput.AutomationControllerInstance.AutomationDriverInstanceCollection is not null
                    && fullUpdateAutomationControllerInstanceOutput.AutomationControllerInstance.AutomationDriverInstanceCollection.Count > 0)
                {
                    List<string> automationConfigurationsToUpdate = Input["AutomationConfigurationsToUpdate"] as List<string>;

                    IAutomationDriverInstance updatedAutomationDriverInstance = entityFactory.Create<IAutomationDriverInstance>();
                    updatedAutomationDriverInstance.Load(
                        fullUpdateAutomationControllerInstanceOutput.AutomationControllerInstance.AutomationDriverInstanceCollection[0].Name);

                    configurationOrchestration.CustomAutomationUpdateConfigurationEntities(new CustomAutomationUpdateConfigurationEntitiesInput()
                    {
                        AutomationConfigurationsToUpdate = automationConfigurationsToUpdate,
                        UpdatedDriverInstance = updatedAutomationDriverInstance,
                        ActionGroupName = Input["ActionGroupName"] as string
                    });
                }
                Input.Remove("AutomationConfigurationsToUpdate");
            }

            //---End DEE Code---
            return Input;
        }

        public override bool DeeTestCondition(Dictionary<string, object> Input)
        {
            //---Start DEE Condition Code---

            //If the current action group is FullUpdateAutomationControllerInstance.Post only execute DEE if there are AutomationConfigurations to update
            if (Input["ActionGroupName"] as string == "ConnectIoTManagement.ConnectIoTManagementOrchestration.FullUpdateAutomationControllerInstance.Post")
            {
                List<string> automationConfigurationsToUpdate =
                    ApplicationContext.CallContext.GetInformationContext("AutomationConfigurationsToUpdate") as List<string>;

                if (automationConfigurationsToUpdate is null || automationConfigurationsToUpdate.Count == 0)
                {
                    return false;
                }

                Input.Add("AutomationConfigurationsToUpdate", automationConfigurationsToUpdate);
            }
            return true;
            //---End DEE Condition Code---
        }
    }
}
