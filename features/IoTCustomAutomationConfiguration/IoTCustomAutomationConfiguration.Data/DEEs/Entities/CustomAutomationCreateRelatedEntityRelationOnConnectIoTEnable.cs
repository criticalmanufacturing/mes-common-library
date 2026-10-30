using Cmf.Community.IoTCustomAutomationConfiguration.Actions.Orchestration.InputObjects;
using Cmf.Community.IoTCustomAutomationConfiguration.Actions.Orchestration;
using Cmf.Foundation.BusinessOrchestration.EntityTypeManagement.OutputObjects;
using Cmf.Foundation.Common.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Cmf.Community.IoTCustomAutomationConfiguration.Actions.Actions.Entities
{
    public class CustomAutomationCreateRelatedEntityRelationOnConnectIoTEnable : DeeDevBase
    {
        public override Dictionary<string, object> DeeActionCode(Dictionary<string, object> Input)
        {
            //---Start DEE Code---
            UseReference("", "System");
            UseReference("", "System.Collections.Generic");
            UseReference("", "Microsoft.Extensions.DependencyInjection");
            UseReference("Cmf.Common.CustomActionUtilities.dll", "Cmf.Common.CustomActionUtilities.Abstractions");
            UseReference("Cmf.Common.CustomActionUtilities.dll", "Cmf.Common.CustomActionUtilities");
            UseReference("", "System.Data");
            UseReference("Cmf.Community.IoTCustomAutomationConfiguration.Actions.Common.dll", "Cmf.Community.IoTCustomAutomationConfiguration.Actions.Common.Extensions");
            UseReference("Cmf.Community.IoTCustomAutomationConfiguration.Actions.Orchestration.dll", "Cmf.Community.IoTCustomAutomationConfiguration.Actions.Orchestration");
            UseReference("Cmf.Community.IoTCustomAutomationConfiguration.Actions.Orchestration.dll", "Cmf.Community.IoTCustomAutomationConfiguration.Actions.Orchestration.InputObjects");
            UseReference("Cmf.Foundation.BusinessOrchestration.dll", "Cmf.Foundation.BusinessOrchestration.EntityTypeManagement.OutputObjects");


            IServiceProvider serviceProvider = (IServiceProvider)Input["ServiceProvider"];
            IEntityFactory entityFactory = serviceProvider.GetService<IEntityFactory>();
            ICustomAutomationConfigurationOrchestration configurationOrchestration = serviceProvider.GetService<ICustomAutomationConfigurationOrchestration>();


            var entityTypeToCreateRelation = (Input["FullUpdateEntityTypeOutput"] as FullUpdateEntityTypeOutput).EntityType;

            configurationOrchestration.CustomAutomationCreateRelatedEntityRelationOnConnectIoTEnabled(new CustomAutomationCreateRelatedEntityRelationOnConnectIoTEnabledInput()
            {
                EntityType = entityTypeToCreateRelation
            });
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
