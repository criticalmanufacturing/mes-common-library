using Cmf.Foundation.BusinessObjects;
using Cmf.Foundation.BusinessOrchestration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;

namespace Cmf.Community.IoTCustomAutomationConfiguration.Actions.Orchestration.InputObjects
{
    [DataContract(Name = "CustomAutomationCreateConfigurationMetadataInput")]
    public class CustomAutomationCreateConfigurationMetadataInput : BaseInput
    {
        [DataMember(Name = "AutomationProtocol", Order = 1)]
        public AutomationProtocol AutomationProtocol { get; set; }
    }
}
