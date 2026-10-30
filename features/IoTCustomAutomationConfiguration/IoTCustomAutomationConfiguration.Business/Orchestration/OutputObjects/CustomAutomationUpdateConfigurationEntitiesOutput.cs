using Cmf.Foundation.BusinessOrchestration;
using System.Collections.Generic;
using System.Runtime.Serialization;

namespace Cmf.Community.IoTCustomAutomationConfiguration.Actions.Orchestration.OutputObjects
{
    [DataContract(Name = "CustomAutomationUpdateConfigurationEntitiesOutput")]
    public class CustomAutomationUpdateConfigurationEntitiesOutput : BaseOutput
    {
        [DataMember(Name = "AutomationConfigurationsToUpdate", Order = 1)]
        public List<string> AutomationConfigurationsToUpdate;
    }
}