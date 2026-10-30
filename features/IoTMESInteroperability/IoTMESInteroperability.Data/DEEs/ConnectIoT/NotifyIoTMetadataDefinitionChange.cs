using System;
using System.Collections.Generic;
using Cmf.Foundation.Common;
using Cmf.Foundation.BusinessObjects.SmartTables;
using Cmf.Community.IoTMESInteroperability.Common;
using Cmf.Community.IoTMESInteroperability.Utilities;

namespace Cmf.Community.IoTMESInteroperability.Actions
{
    public class NotifyIoTMetadataDefinitionChange : DeeDevBase
    {
        public override bool DeeTestCondition(Dictionary<string, object> Input)
        {
            //---Start DEE Condition Code---

            #region Info

            /// <summary>
            /// Summary text
            ///     *NOTE: Workaround for table remove row validation*
            ///     Action to notify Connect IoT when there is a change in SmartTable IoTMetadataDefinition
            /// Action Groups:
            ///     SmartTables.SmartTables.InsertOrUpdateRows.Post
            ///     SmartTables.SmartTables.RemoveRows.Post
            /// Depends On:
            /// Is Dependency For:
            /// Exceptions:
            /// </summary>

            #endregion

            return true;

            //---End DEE Condition Code---

        }

        public override Dictionary<string, object> DeeActionCode(Dictionary<string, object> Input)
        {
            //---Start DEE Code---

            // CORE
            UseReference("", "Cmf.Foundation.Common");
            UseReference("", "Cmf.Foundation.BusinessObjects.SmartTables");

            // Custom
            UseReference("Cmf.Community.IoTMESInteroperability.Common.dll", "Cmf.Community.IoTMESInteroperability.Common");
            UseReference("Cmf.Community.IoTMESInteroperability.Common.dll", "Cmf.Community.IoTMESInteroperability");

            string targetSmartTableName = IoTUtilitiesConstants.IoTMetadataDefinition;

            // Only send the message via message bus for IoT to invalid cache if the 
            // insertorupdaterows is being performed at the target SmartTable
            // or the removerows is being performed at the target SmartTable
            if ((Input.ContainsKey("ActionGroupName") &&
                Input["ActionGroupName"] != null &&
                Input["ActionGroupName"].ToString().Equals("SmartTables.SmartTables.InsertOrUpdateRows.Post", StringComparison.InvariantCultureIgnoreCase) &&
                Input.ContainsKey(Cmf.Foundation.Common.Constants.SmartTable) &&
                Input[Cmf.Foundation.Common.Constants.SmartTable] != null) ||
                (Input.ContainsKey("ActionGroupName") &&
                Input["ActionGroupName"] != null &&
                Input["ActionGroupName"].ToString().Equals("SmartTables.SmartTables.RemoveRows.Post", StringComparison.InvariantCultureIgnoreCase) &&
                Input.ContainsKey(Cmf.Foundation.Common.Constants.SmartTable) &&
                Input[Cmf.Foundation.Common.Constants.SmartTable] != null))
            {

                // Confirm table
                SmartTable smartTable = IoTUtilities.GetInputItem<SmartTable>(Input, Cmf.Foundation.Common.Constants.SmartTable);

                if (smartTable != null &&
                    !string.IsNullOrEmpty(smartTable.Name) &&
                    targetSmartTableName.Equals(smartTable.Name))
                {
                    const string Subject = "CMF.IoT.Utilities.MetadataChange";
                    const string Message = "NewValueIoTMetadata";

                    // Broadcast a message via MessageBus
                    // The Automation should check this as a cache invalidation message and clean the cache
                    Cmf.Foundation.Common.Utilities.PublishMessage(Subject, Message.ToJsonString());
                }
            }

            Input.Add("Result", true);

            //---End DEE Code---

            return null;
        }
    }
}
