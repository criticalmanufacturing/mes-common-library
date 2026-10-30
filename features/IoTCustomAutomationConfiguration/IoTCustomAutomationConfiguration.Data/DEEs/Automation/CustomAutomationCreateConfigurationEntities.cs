using Cmf.Foundation.BusinessObjects;
using Cmf.Foundation.BusinessOrchestration;
using Cmf.Foundation.BusinessOrchestration.ConnectIoTManagement.OutputObjects;
using Cmf.Foundation.Common;
using Cmf.Foundation.Common.Abstractions;
using System;
using System.ComponentModel;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Microsoft.Extensions.DependencyInjection;
using Cmf.Community.IoTCustomAutomationConfiguration.Actions.Orchestration;
using Cmf.Community.IoTCustomAutomationConfiguration.Actions.Orchestration.InputObjects;

namespace Cmf.Community.IoTCustomAutomationConfiguration.Actions.Actions.Automation
{
    public class CustomAutomationCreateConfigurationEntities : DeeDevBase
    {
        public override Dictionary<string, object> DeeActionCode(Dictionary<string, object> Input)
        {
            //---Start DEE Code---
            //Foundation
            UseReference("Cmf.Foundation.BusinessObjects.dll", "Cmf.Foundation.BusinessObjects");
            UseReference("Cmf.Foundation.BusinessOrchestration.dll", "Cmf.Foundation.BusinessOrchestration");
            UseReference("Cmf.Foundation.BusinessOrchestration.dll", "Cmf.Foundation.BusinessOrchestration.ConnectIoTManagement.OutputObjects");
            UseReference("Cmf.Foundation.Common.dll", "Cmf.Foundation.Common");
            UseReference("Cmf.Foundation.Common.dll", "Cmf.Foundation.Common.Abstractions");

            //System
            UseReference("", "System");
            UseReference("", "System.ComponentModel");
            UseReference("", "System.Collections.Generic");
            UseReference("", "System.Collections.ObjectModel");
            UseReference("", "Microsoft.Extensions.DependencyInjection");

            //Custom
            UseReference("Cmf.Community.IoTCustomAutomationConfiguration.Actions.Orchestration.dll", "Cmf.Community.IoTCustomAutomationConfiguration.Actions.Orchestration");
            UseReference("Cmf.Community.IoTCustomAutomationConfiguration.Actions.Orchestration.dll", "Cmf.Community.IoTCustomAutomationConfiguration.Actions.Orchestration.InputObjects");


            IServiceProvider serviceProvider = (IServiceProvider)Input["ServiceProvider"];
            IEntityFactory entityFactory = serviceProvider.GetService<IEntityFactory>();
            ICustomAutomationConfigurationOrchestration configurationOrchestration = serviceProvider.GetService<ICustomAutomationConfigurationOrchestration>();

            if (configurationOrchestration == null)
            {
                return Input;
            }

            if (Input.ContainsKey("CreateAutomationControllerInstanceOutput"))
            {
                var configurationList = ApplicationContext.CallContext.GetInformationContext("CreatedAutomationConfigurationEntities") as Collection<object>;
                CreateAutomationControllerInstanceOutput createAutomationControllerInstanceOutput = Input["CreateAutomationControllerInstanceOutput"] as CreateAutomationControllerInstanceOutput;


                var list = new BindingList<object>();
                createAutomationControllerInstanceOutput.FeedbackMessages = new Collection<FeedbackMessage>();

                FeedbackMessage feedbackMessage = new FeedbackMessage
                {
                    InnerMessage = "Please configure the communication setting by using the following entities.",
                    MessageType = FeedbackMessageType.ObjectsToOpen,
                    Objects = new Collection<object>()
                };

                foreach (var configuration in configurationList)
                {
                    feedbackMessage.Objects.Add(configuration);

                }

                createAutomationControllerInstanceOutput.FeedbackMessages.Add(feedbackMessage);

            }
            else
            {
                CustomAutomationCreateConfigurationEntitiesInput customAutomationCreateConfigurationEntitiesInput = new CustomAutomationCreateConfigurationEntitiesInput()
                {
                    AutomationControllerInstance = Input["AutomationControllerInstance"] as AutomationControllerInstance,
                    AutomationDriverInstanceCollection = Input["AutomationDriverInstanceCollection"] as AutomationDriverInstanceCollection
                };
                var result = configurationOrchestration.CustomAutomationCreateConfigurationEntities(customAutomationCreateConfigurationEntitiesInput);
            }
            //---End DEE Code---
            return Input;
        }

        public override bool DeeTestCondition(Dictionary<string, object> Input)
        {
            //---Start DEE Condition Code---
            return true;
            //---End DEE Condition Code---
        }
    }
}
