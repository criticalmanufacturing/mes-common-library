using Cmf.Foundation.BusinessObjects;
using Cmf.Foundation.BusinessObjects.Abstractions;
using Cmf.Foundation.BusinessOrchestration;
using Cmf.Navigo.BusinessObjects;
using Cmf.Navigo.BusinessObjects.Abstractions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;

namespace Cmf.Community.IoTCustomAutomationConfiguration.Actions.Orchestration.OutputObjects
{
    [DataContract(Name = "CustomAutomationCreateConfigurationMetadataOutput")]
    public class CustomAutomationCreateConfigurationMetadataOutput : BaseOutput
    {
        [DataMember(Name = "AutomationProtocol", Order = 1)]
        public IAutomationProtocol AutomationProtocol { get; set; }
        [DataMember(Name = "ParameterCollection", Order = 2)]
        public IParameterCollection ParameterCollection { get; set; }
    }
}
