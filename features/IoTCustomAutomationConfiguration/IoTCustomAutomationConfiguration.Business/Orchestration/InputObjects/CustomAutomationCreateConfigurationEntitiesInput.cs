using Cmf.Foundation.BusinessObjects.Abstractions;
using Cmf.Foundation.BusinessOrchestration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;

namespace Cmf.Community.IoTCustomAutomationConfiguration.Actions.Orchestration.InputObjects
{
    [DataContract(Name = "CustomAutomationCreateConfigurationEntitiesInput")]
    public class CustomAutomationCreateConfigurationEntitiesInput : BaseInput
    {
        [DataMember(Name = "AutomationControllerInstance", Order = 1)]
        public IAutomationControllerInstance AutomationControllerInstance;
        [DataMember(Name = "AutomationDriverInstanceCollection", Order = 2)]
        public IAutomationDriverInstanceCollection AutomationDriverInstanceCollection;

    }
}
