using Cmf.Community.IoTCustomAutomationConfiguration.Actions.Common.DataStructures;
using Cmf.Foundation.BusinessOrchestration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;

namespace Cmf.Community.IoTCustomAutomationConfiguration.Actions.Orchestration.OutputObjects
{
    [DataContract(Name = "CustomAutomationRetriveConfigurationOutput")]
    public class CustomAutomationRetrieveConfigurationOutput : BaseOutput
    {
        [DataMember(Name = "RelatedEntityName", Order = 1)]
        public string RelatedEntityName;
        [DataMember(Name = "RelatedEntityTypeName", Order = 2)]
        public string RelatedEntityTypeName;
        [DataMember(Name = "RetrieveRelatedController", Order = 3)]
        public bool? RetrieveControllerConfiguration;
        [DataMember(Name = "AutomationConfigurationData", Order = 4)]
        public AutomationConfigurationData AutomationConfigurationData;
    }
}
