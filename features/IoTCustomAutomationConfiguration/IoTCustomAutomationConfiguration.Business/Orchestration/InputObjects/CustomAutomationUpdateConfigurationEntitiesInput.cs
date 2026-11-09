using Cmf.Foundation.BusinessObjects.Abstractions;
using Cmf.Foundation.BusinessOrchestration;
using System.Collections.Generic;
using System.Runtime.Serialization;

namespace Cmf.Community.IoTCustomAutomationConfiguration.Actions.Orchestration.InputObjects
{
    [DataContract(Name = "CustomAutomationUpdateConfigurationEntitiesInput")]
    public class CustomAutomationUpdateConfigurationEntitiesInput : BaseInput
    {
        [DataMember(Name = "ActionGroupName", Order = 1)]
        public string ActionGroupName;
        [DataMember(Name = "AutomationConfigurationsToUpdate", Order = 2)]
        public List<string> AutomationConfigurationsToUpdate;
        [DataMember(Name = "UpdatedDriverInstance", Order = 3)]
        public IAutomationDriverInstance UpdatedDriverInstance;
    }
}