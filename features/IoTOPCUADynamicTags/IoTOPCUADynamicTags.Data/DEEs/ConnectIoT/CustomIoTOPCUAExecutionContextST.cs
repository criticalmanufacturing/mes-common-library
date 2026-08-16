using System.Collections.Generic;
using System.Data;
using Cmf.Foundation.BusinessObjects.Abstractions;
using Cmf.Foundation.BusinessObjects;
using System.Linq;
using System;
using Cmf.Foundation.BusinessObjects.SmartTables;

namespace Cmf.Community.IoTOPCUADynamicTags.Actions.ConnectIoT
{
    public class CustomIoTOPCUAExecutionContextST : DeeDevBase
    {
        public override bool DeeTestCondition(Dictionary<string, object> Input)
        {
            //---Start DEE Condition Code---

            #region Info

            /// <summary>
            ///     Validates the data added in CustomIoTOPCUAExecutionContext ST.
            /// 
            /// Action Groups:
            ///     - Pre-Validation Rule on CustomIoTOPCUAExecutionContext Smart Table
            /// Depends On:
            /// Is Dependency For:
            /// Exceptions:
            /// </summary>

            #endregion

            if (Input != null &&
                Input.ContainsKey("SmartTable") &&
                Input["SmartTable"] != null &&
                Input.ContainsKey("TableData") &&
                Input["TableData"] != null)
            {
                return true;
            }

            return false;

            //---End DEE Condition Code---
        }

        public override Dictionary<string, object> DeeActionCode(Dictionary<string, object> Input)
        {
            //---Start DEE Code---

            // System
            UseReference("", "System.Data");
            UseReference("%MicrosoftNetPath%\\System.Xml.dll", "System.Xml");
            UseReference("%MicrosoftNetPath%\\System.Xml.Linq.dll", "System.Xml.Linq");

            // CORE
            UseReference("Cmf.Foundation.BusinessOrchestration.dll", "");
            UseReference("Cmf.Foundation.BusinessObjects.dll", "Cmf.Foundation.BusinessObjects.SmartTables");

            // MES
            UseReference("Cmf.Navigo.BusinessObjects.dll", "Cmf.Navigo.BusinessObjects");
            UseReference("Cmf.Navigo.BusinessObjects.dll", "Cmf.Navigo.BusinessObjects.Abstractions");

            // Temporary fix: Removed reference to Cmf.Common.CustomActionUtilities.dll is not available for v11.

            #region Building Reference Table
            // This reference table idEventTrigger will be used to ensure
            // that there can only be one and only one TriggerOnChange row for any Event

            SmartTable st = Input["SmartTable"] as SmartTable;
            st.Load();
            st.LoadData();
            DataSet dsSmartTable = NgpDataSet.ToDataSet(st.Data);

            List<DataRow> idEventTrigger = new List<DataRow>();

            if (dsSmartTable.Tables != null && dsSmartTable.Tables.Count > 0)
            {
                DataTable dataTable = dsSmartTable.Tables[0];
                if (dataTable != null)
                {
                    foreach (DataRow row in dataTable.Rows)
                    {
                        idEventTrigger.Add(row);
                    }
                }
            }

            #endregion Building Reference Table

            INgpDataSet tableDataSet = Input["TableData"] as INgpDataSet;
            DataSet entries = NgpDataSet.ToDataSet(tableDataSet);

            if (entries.Tables != null && entries.Tables.Count > 0)
            {
                DataTable dataTable = entries.Tables[0];
                if (dataTable != null)
                {
                    foreach (DataRow row in dataTable.Rows)
                    {
                        // Temporary fix: Cmf.Common.CustomActionUtilities.dll is not available for v11.
                        // List<string> rowKeys = row.GetKeyValueDictionary().Keys.ToList();

                        // #region Validate                        

                        // if (bool.Parse(row["ExecDee"].ToString()))
                        // {
                        //     if (bool.Parse(row["ExecService"].ToString()))
                        //     {
                        //         GeneralUtilities.ThrowLocalizedException("CustomIoTDataCollectionSTWrongValue", "ExecService", (bool.Parse(row["ExecService"].ToString())).ToString(), "ExecDee");
                        //     }
                        //     if (rowKeys.Contains("ExecDefinition") && row["ExecDefinition"].ToString().IsNullOrEmpty())
                        //     {
                        //         GeneralUtilities.ThrowLocalizedException("CustomIoTDataCollectionSTNotEmptyExpected", "ExecDefinition", "ExecDee");
                        //     }
                        //     if (rowKeys.Contains("ExecDefinition") && row["ExecDefinition"].ToString().Contains("|"))
                        //     {
                        //         GeneralUtilities.ThrowLocalizedException("CustomIoTDataCollectionSTWrongValue", "ExecDefinition", "|", "ExecService");
                        //     }
                        // }

                        // if (bool.Parse(row["ExecService"].ToString()))
                        // {
                        //     if (bool.Parse(row["ExecDee"].ToString()))
                        //     {
                        //         GeneralUtilities.ThrowLocalizedException("CustomIoTDataCollectionSTWrongValue", "ExecDee", (bool.Parse(row["ExecDee"].ToString())).ToString(), "ExecService");
                        //     }
                        //     if (rowKeys.Contains("ExecDefinition") && row["ExecDefinition"].ToString().IsNullOrEmpty())
                        //     {
                        //         GeneralUtilities.ThrowLocalizedException("CustomIoTDataCollectionSTNotEmptyExpected", "ExecDefinition", "ExecService");
                        //     }
                        //     if (rowKeys.Contains("ExecDefinition") && !row["ExecDefinition"].ToString().Contains("|"))
                        //     {
                        //         GeneralUtilities.ThrowLocalizedException("CustomIoTDataCollectionSTWrongValue", "ExecDefinition", "without |", "ExecService");
                        //     }

                        // }

                        #region Populating the idEventTrigger row with updated rows

                        var refRow = idEventTrigger.FirstOrDefault(x => x["CustomIoTOPCUAExecutionContextId"].ToString() == row["CustomIoTOPCUAExecutionContextId"].ToString());

                        if (refRow != null)
                        {
                            idEventTrigger.Remove(refRow);
                        }

                        idEventTrigger.Add(row);
                        #endregion

                        //#endregion Validate
                    }

                    #region Checking for uniqueness of TriggerOnChange row

                    Dictionary<string, int> triggerOnChange = new Dictionary<string, int>();
                    foreach (DataRow row in idEventTrigger)
                    {
                        if (!triggerOnChange.ContainsKey(row["Event"].ToString()))
                        {
                            triggerOnChange.Add(row["Event"].ToString(), bool.Parse(row["TriggerOnChange"].ToString()) ? 1 : 0);
                        }
                        else if (bool.Parse(row["TriggerOnChange"].ToString()))
                        {
                            var item = triggerOnChange.First(x => x.Key == row["Event"].ToString());
                            triggerOnChange[row["Event"].ToString()] = item.Value + 1;
                        }
                    }

                    if (triggerOnChange.Any(x => x.Value < 1))
                    {
                        throw new Exception("There must be one Trigger on Event set per Event.");
                    }

                    if (triggerOnChange.Any(x => x.Value > 1))
                    {
                        throw new Exception("There can be only one Trigger on Event per Event.");
                    }

                    #endregion Checking for uniqueness of TriggerOnChange row
                }
            }

            Input.Add("Result", true);
            //---End DEE Code---

            return Input;
        }
    }
}