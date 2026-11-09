using Cmf.Foundation.Common.Abstractions;
using System;
using System.Linq;
using System.Collections.Generic;
using Microsoft.Extensions.DependencyInjection;
using Cmf.Community.IoTCustomAutomationConfiguration.Actions.Common.DataStructures;
using Cmf.Community.IoTCustomAutomationConfiguration.Actions.Orchestration;
using Cmf.Community.IoTCustomAutomationConfiguration.Actions.Orchestration.InputObjects;

namespace Cmf.Community.IoTCustomAutomationConfiguration.Actions.Actions.Automation
{
    public class CustomAutomationRetrieveConfigurationData : DeeDevBase
    {
        public override Dictionary<string, object> DeeActionCode(Dictionary<string, object> Input)
        {
            //---Start DEE Code---
            //Foundation
            UseReference("Cmf.Foundation.Common.dll", "Cmf.Foundation.Common.Abstractions");

            //System
            UseReference("", "System");
            UseReference("", "System.Linq");
            UseReference("", "System.Collections.Generic");
            UseReference("", "Microsoft.Extensions.DependencyInjection");

            //Custom
            UseReference("Cmf.Community.IoTCustomAutomationConfiguration.Actions.Common.dll", "Cmf.Community.IoTCustomAutomationConfiguration.Actions.Common.DataStructures");
            UseReference("Cmf.Community.IoTCustomAutomationConfiguration.Actions.Orchestration.dll", "Cmf.Community.IoTCustomAutomationConfiguration.Actions.Orchestration");
            UseReference("Cmf.Community.IoTCustomAutomationConfiguration.Actions.Orchestration.dll", "Cmf.Community.IoTCustomAutomationConfiguration.Actions.Orchestration.InputObjects");

            IServiceProvider serviceProvider = (IServiceProvider)Input["ServiceProvider"];
            IEntityFactory entityFactory = serviceProvider.GetService<IEntityFactory>();
            ICustomAutomationConfigurationOrchestration configurationOrchestration = serviceProvider.GetService<ICustomAutomationConfigurationOrchestration>();

            string instanceEntityName = Input["InstanceName"] as string;
            string InstanceEntityTypeName = Input["InstanceEntityTypeName"] as string;
            bool loadControlerConfiguration = true;
            if (Input.ContainsKey("LoadControlerConfiguration"))
            {
                loadControlerConfiguration = (Input["LoadControlerConfiguration"] as bool?).Value;
            }


            var customAutomationRetriveConfigurationInput = new CustomAutomationRetrieveConfigurationInput()
            {
                RelatedEntityName = instanceEntityName,
                RelatedEntityTypeName = InstanceEntityTypeName.Split(",").First().Split(".").Last(),
                RetrieveControllerConfiguration = loadControlerConfiguration
            };
            var retrievedConfiguration = configurationOrchestration.CustomAutomationRetrieveConfiguration(customAutomationRetriveConfigurationInput);

            Input["RetrivedConfigurationData"] = retrievedConfiguration.AutomationConfigurationData;
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
