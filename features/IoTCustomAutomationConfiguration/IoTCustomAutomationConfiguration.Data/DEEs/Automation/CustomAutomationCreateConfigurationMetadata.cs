using Cmf.Foundation.BusinessObjects;
using Cmf.Foundation.Common.Abstractions;
using System;
using System.Collections.Generic;
using Microsoft.Extensions.DependencyInjection;
using Cmf.Community.IoTCustomAutomationConfiguration.Actions.Orchestration;
using Cmf.Community.IoTCustomAutomationConfiguration.Actions.Orchestration.InputObjects;

namespace Cmf.Community.IoTCustomAutomationConfiguration.Actions.Actions.Automation
{
    public class CustomAutomationCreateConfigurationMetadata : DeeDevBase
    {

        public override Dictionary<string, object> DeeActionCode(Dictionary<string, object> Input)
        {
            //---Start DEE Code---
            //Foundation
            UseReference("Cmf.Foundation.BusinessObjects.dll", "Cmf.Foundation.BusinessObjects");
            UseReference("Cmf.Foundation.Common.dll", "Cmf.Foundation.Common.Abstractions");

            //System
            UseReference("", "System");
            UseReference("", "System.Collections.Generic");
            UseReference("", "Microsoft.Extensions.DependencyInjection");

            //Custom
            UseReference("Cmf.Community.IoTCustomAutomationConfiguration.Actions.Orchestration.dll", "Cmf.Community.IoTCustomAutomationConfiguration.Actions.Orchestration");
            UseReference("Cmf.Community.IoTCustomAutomationConfiguration.Actions.Orchestration.dll", "Cmf.Community.IoTCustomAutomationConfiguration.Actions.Orchestration.InputObjects");

            IServiceProvider serviceProvider = (IServiceProvider)Input["ServiceProvider"];
            IEntityFactory entityFactory = serviceProvider.GetService<IEntityFactory>();
            ICustomAutomationConfigurationOrchestration configurationOrchestration = serviceProvider.GetService<ICustomAutomationConfigurationOrchestration>();

            AutomationProtocolCollection automationProtocolCollection = Input["AutomationProtocolCollection"] as AutomationProtocolCollection;

            foreach (AutomationProtocol automationProtocol in automationProtocolCollection)
            {

                var customAutomationCreateConfigurationMetadataInput = new CustomAutomationCreateConfigurationMetadataInput()
                {
                    AutomationProtocol = automationProtocol
                };
                var result = configurationOrchestration.CustomAutomationCreateConfigurationMetadata(customAutomationCreateConfigurationMetadataInput);

            }
            //---End DEE Code---
            return Input;

        }

        public override bool DeeTestCondition(Dictionary<string, object> Input)
        {
            //---Start DEE Condition Code---
            if (!Input.TryGetValue("ActionGroupName", out var actionGroupName) ||
                !string.Equals(actionGroupName?.ToString(), "BusinessObjects.AutomationProtocolCollection.CreateVersion.Post", StringComparison.Ordinal))
            {
                return false;
            }

            return true;

            //---End DEE Condition Code---
        }
    }
}
