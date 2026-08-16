using Cmf.Foundation.BusinessOrchestration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;

namespace Cmf.Community.IoTMESInteroperability.Orchestration.OutputObjects
{
    /// <summary>
    /// Output Data Contract for the IoTMaterialOperation service
    /// </summary>
    [DataContract(Name = "IoTMaterialOperationOutput")]
    public class IoTMaterialOperationOutput : BaseOutput
    {
        /// <summary>
        /// Material Name
        /// </summary>
        [DataMember(Name = "MaterialName", Order = 0)]
        public string MaterialName { get; set; }

    }
}
