using Cmf.Foundation.BusinessObjects;
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
    [DataContract(Name = "CustomAutomationCreateRelatedEntityRelationOnConnectIoTEnabledOutput")]
    public class CustomAutomationCreateRelatedEntityRelationOnConnectIoTEnabledOutput : BaseOutput
    {
        [DataMember(Name = "EntityType", Order = 1)]
        public IEntityType EntityType { get; set; }
        [DataMember(Name = "RelatedEntityTypeCreated", Order = 1)]
        public IEntityType RelatedEntityTypeCreated { get; set; }
    }
}
