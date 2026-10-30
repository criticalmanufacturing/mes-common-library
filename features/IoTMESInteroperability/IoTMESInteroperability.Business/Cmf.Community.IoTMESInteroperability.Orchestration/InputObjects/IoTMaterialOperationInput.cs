using Cmf.Foundation.BusinessOrchestration;
using Cmf.Navigo.BusinessObjects;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;

namespace Cmf.Community.IoTMESInteroperability.Orchestration.InputObjects
{
    /// <summary>
    /// Input Data Contract for the IoTMaterialOperation service
    /// </summary>
    [DataContract(Name = "IoTMaterialOperationInput")]
    public class IoTMaterialOperationInput : BaseInput
    {
        /// <summary>
        /// </summary>
        [DataMember(Name = "Operation", Order = 0)]
        public string Operation { get; set; }

        /// <summary>
        /// </summary>
        [DataMember(Name = "Resource", Order = 1)]
        public Resource Resource { get; set; }

        /// <summary>
        /// </summary>
        [DataMember(Name = "Material", Order = 2)]
        public Material Material { get; set; }

        /// <summary>
        /// </summary>
        [DataMember(Name = "Materials", Order = 3)]
        public MaterialCollection Materials { get; set; }

    }
}


