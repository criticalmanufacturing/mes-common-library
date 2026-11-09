using Cmf.Foundation.BusinessOrchestration;
using System.Runtime.Serialization;

namespace Cmf.Community.IoTMESInteroperability.Orchestration.OutputObjects
{
    /// <summary>
    /// Output Data Contract for the GetMaterialRecipe service
    /// </summary>
    [DataContract(Name = "GetMaterialRecipeOutput")]
    public class GetMaterialRecipeOutput : BaseOutput
    {
        /// <summary>
        /// RecipeName
        /// </summary>
        [DataMember(Name = "RecipeName", Order = 0)]
        public string RecipeName { get; set; }

        /// <summary>
        /// MaterialName
        /// </summary>
        [DataMember(Name = "MaterialName", Order = 1)]
        public string MaterialName { get; set; }

        /// <summary>
        /// ResourceName
        /// </summary>
        [DataMember(Name = "ResourceName", Order = 2)]
        public string ResourceName { get; set; }

        /// <summary>
        /// ResourceRecipeName
        /// </summary>
        [DataMember(Name = "ResourceRecipeName", Order = 3)]
        public string ResourceRecipeName { get; set; }

        /// <summary>
        /// ProductName
        /// </summary>
        [DataMember(Name = "ProductName", Order = 4)]
        public string ProductName { get; set; }
    }
}
