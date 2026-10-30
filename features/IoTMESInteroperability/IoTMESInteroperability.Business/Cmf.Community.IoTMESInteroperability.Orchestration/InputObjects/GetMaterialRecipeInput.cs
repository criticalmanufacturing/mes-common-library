using Cmf.Foundation.BusinessOrchestration;
using System.Runtime.Serialization;

namespace Cmf.Community.IoTMESInteroperability.Orchestration.InputObjects
{
    /// <summary>
    /// Input Data Contract for the GetMaterialRecipe service
    /// </summary>
    [DataContract(Name = "GetMaterialRecipeInput")]
    public class GetMaterialRecipeInput : BaseInput
    {
        /// <summary>
        /// ResourceName
        /// </summary>
        [DataMember(Name = "ResourceName", Order = 0)]
        public string ResourceName { get; set; }

        /// <summary>
        /// MaterialName
        /// </summary>
        [DataMember(Name = "MaterialName", Order = 1)]
        public string MaterialName { get; set; }
    }
}
