using System.Collections.Generic;
using Cmf.Foundation.Common;
using Cmf.Community.IoTMESInteroperability.Common;
using Cmf.Community.IoTMESInteroperability.Utilities;
using Cmf.Foundation.BusinessObjects.Abstractions;

namespace Cmf.Community.IoTMESInteroperability.Actions
{
    public class NotifyMetadataChange : DeeDevBase
    {
        public override bool DeeTestCondition(Dictionary<string, object> Input)
        {
            //---Start DEE Condition Code---

            #region Info

            /// <summary>
            /// Summary text
            ///     Action to notify Connect IoT when there is a change in SmartTable ÍoTMetadataDefinition
            /// Action Groups:
            /// </summary>
            /// 
            #endregion

            bool isToExecute = false;

            const string CustomMachineStateContext = IoTUtilitiesConstants.IoTMetadataDefinition;

            if (Input.ContainsKey(Cmf.Foundation.Common.Constants.SmartTable))
            {
                // Validate SmartTable from Input
                ISmartTable smartTable = IoTUtilities.GetInputItem<ISmartTable>(Input, Cmf.Foundation.Common.Constants.SmartTable);

                if (smartTable != null && !string.IsNullOrEmpty(smartTable.Name))
                {
                    isToExecute = CustomMachineStateContext.Equals(smartTable.Name);
                }
            }

            return isToExecute;

            //---End DEE Condition Code---

        }

        public override Dictionary<string, object> DeeActionCode(Dictionary<string, object> Input)
        {
            //---Start DEE Code---

            // CORE
            UseReference("", "Cmf.Foundation.Common");
            UseReference("", "Cmf.Foundation.BusinessObjects.Abstractions");

            // Custom
            UseReference("Cmf.Community.IoTMESInteroperability.Common.dll", "Cmf.Community.IoTMESInteroperability.Common");
            UseReference("Cmf.Community.IoTMESInteroperability.Common.dll", "Cmf.Community.IoTMESInteroperability");

            const string Subject = "CMF.IoT.Utilities.MetadataChange";
            const string Message = "NewValueIoTMetadata";

            // BroadCasts a message via MessageBus
            // The Automation should check this as a cache invalidation message and clean the cache
            Cmf.Foundation.Common.Utilities.PublishMessage(Subject, Message.ToJsonString());
            Dictionary<string, object> outputDEE = new Dictionary<string, object>();
            outputDEE.Add("Result", true);
            return (outputDEE);

            //---End DEE Code---
        }
    }
}
