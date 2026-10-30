using Cmf.Community.IoTCustomAutomationConfiguration.Actions.Common.BusinessObjects.Abstractions;
using Cmf.Foundation.BusinessObjects.Abstractions;
using Cmf.Foundation.BusinessOrchestration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;

namespace Cmf.Community.IoTCustomAutomationConfiguration.Actions.Orchestration.OutputObjects
{
    [DataContract(Name = "CustomAutomationCreateConfigurationEntitiesOutput")]
    public class CustomAutomationCreateConfigurationEntitiesOutput : BaseOutput
    {
        [DataMember(Name = "AutomationControllerInstance", Order = 1)]
        public IAutomationControllerInstance AutomationControllerInstance;
        [DataMember(Name = "AutomationDriverInstanceCollection", Order = 2)]
        public IAutomationDriverInstanceCollection AutomationDriverInstanceCollection;
        [DataMember(Name = "CustomAutomationConfigurationCollection", Order = 3)]
        public object CustomAutomationConfigurationCollection;
    }
}
