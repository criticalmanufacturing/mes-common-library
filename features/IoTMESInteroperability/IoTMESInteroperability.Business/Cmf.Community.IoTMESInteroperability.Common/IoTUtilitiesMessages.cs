using System;
using System.Collections.Generic;
using System.Text;

namespace Cmf.Community.IoTMESInteroperability.Common
{
    public class IoTUtilitiesMessages
    {
        #region Generic

        /// <summary>
        /// Feature {0} is not enabled
        /// </summary>
        public const string FeatureIsNotEnabled = "FeatureIsNotEnabled";

        /// <summary>
        /// Material is invalid
        /// </summary>
        public const string InvalidMaterial = "InvalidMaterial";

        /// <summary>
        /// Resource is invalid
        /// </summary>
        public const string InvalidResource = "InvalidResource";

        /// <summary>
        /// Inputs are invalid
        /// </summary>
        public const string InvalidInputs = "InvalidInputs";

        /// <summary>
        /// Material Operation is invalid
        /// </summary>
        public const string InvalidMaterialOperation = "InvalidMaterialOperation";

        /// <summary>
        /// No Material found to process
        /// </summary>
        public const string InvalidNoMaterialFound = "InvalidNoMaterialFound";

        /// <summary>
        /// IoTMetadataDefinition (SmartTable)
        /// </summary>
        public const string IoTMetadataDefinition = "IoTMetadataDefinition";


        /// <summary>
        /// $"No Materials InProcess on Operation '{materialOperation}'"
        /// </summary>
        public const string IoTNoMaterialsInProcess = "IoTNoMaterialsInProcess";

        /// <summary>
        /// $"Material '{material.Name}' is not InProcess on Operation '{materialOperation}'"
        /// </summary>
        public const string IoTMaterialNotInProcess = "IoTMaterialNotInProcess";

        /// <summary>
        ///$"No Materials Dispatched/Queued at Resource '{resource.Name}' on Operation '{materialOperation}'"
        /// </summary>
        public const string IoTNoMaterialsDispQueuedAtResource = "IoTNoMaterialsDispQueuedAtResource";


        /// <summary>
        ///$"Material '{material.Name}' is not Dispatched/Queued at Resource '{resource.Name}' on Operation '{materialOperation}'"
        /// </summary>
        public const string IoTMaterialNotDispQueuedAtResource = "IoTMaterialNotDispQueuedAtResource";

        /// <summary>
        /// $"Material '{0}' is not at Resource '{1}' so operation '{2}' is Invalid"
        /// </summary>
        public const string IoTMaterialInvalidMaterialOperationOnResource = "IoTMaterialInvalidMaterialOperationOnResource";


        #endregion Generic
    }
}
