using System;
using System.Collections.Generic;
using System.Linq;
using System.Data;
using Cmf.Foundation.BusinessObjects;
using Cmf.Foundation.BusinessObjects.Abstractions;
using Cmf.Foundation.BusinessOrchestration.Abstractions;
using Cmf.Foundation.Common.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace Cmf.Community.IoTCustomAutomationConfiguration.Actions.Actions.ProcessRules.Baseline.Before
{
    public class CustomClearNameGeneratorContext : DeeDevBase
    {
        public override Dictionary<string, object> DeeActionCode(Dictionary<string, object> Input)
        {
            //---Start DEE Code---
            UseReference("Cmf.Foundation.BusinessOrchestration.dll", "Cmf.Foundation.BusinessOrchestration.Abstractions");
            UseReference("Cmf.Common.CustomActionUtilities.dll", "Cmf.Common.CustomActionUtilities.Abstractions");
            UseReference("Cmf.Foundation.BusinessOrchestration.dll", "Cmf.Foundation.BusinessOrchestration.QueryManagement.InputObjects");
            UseReference("Cmf.Foundation.BusinessOrchestration.dll", "Cmf.Foundation.BusinessOrchestration.QueryManagement.OutputObjects");
            UseReference("refs/System.ObjectModel.dll", "System.Collections.ObjectModel");

            //System
            UseReference("", "System.Data");

            IServiceProvider _serviceProvider = (IServiceProvider)Input["ServiceProvider"];
            IEntityFactory _entityFactory = _serviceProvider.GetService<IEntityFactory>();
            ITableOrchestration _tableOrchestration = _serviceProvider.GetRequiredService<ITableOrchestration>();

            ISmartTable smartTable = _entityFactory.Create<ISmartTable>();
            smartTable.Name = "NameGeneratorContext";
            smartTable.Load();

            // Remove CustomAutomationConfiguration entry
            INgpDataRow smartTableInput = new NgpDataRow();
            smartTableInput.TryAdd("EntityType", "CustomAutomationConfiguration");
            smartTableInput.TryAdd("Operation", "Create");
            smartTableInput.TryAdd("NameGeneratorName", "CustomAutomationConfigurationNameGenerator");

            var resolvedSmartTable = smartTable.Resolve(smartTableInput, true);
            if (resolvedSmartTable != null && resolvedSmartTable.Tables.Any())
            {
                DataRow row = NgpDataSet.ToDataSet(resolvedSmartTable).Tables[0].Rows[0];

                if (!string.IsNullOrWhiteSpace(row["EntityType"]?.ToString()) && row["EntityType"].ToString() == "CustomAutomationConfiguration" &&
                    !string.IsNullOrWhiteSpace(row["Operation"]?.ToString()) && row["Operation"].ToString() == "Create" &&
                    !string.IsNullOrWhiteSpace(row["NameGeneratorName"]?.ToString()) && row["NameGeneratorName"].ToString() == "CustomAutomationConfigurationNameGenerator" &&
                    string.IsNullOrWhiteSpace(row["RevisionGeneratorName"]?.ToString()) &&
                    string.IsNullOrWhiteSpace(row["EntityTypeType"]?.ToString()))
                {
                    _tableOrchestration.RemoveSmartTableRows(new Foundation.BusinessOrchestration.TableManagement.InputObjects.RemoveSmartTableRowsInput()
                    {
                        SmartTable = smartTable,
                        Table = resolvedSmartTable,
                        IgnoreLastServiceId = true,
                    });
                }
            }

            // Remove CustomAutomationConfigurationStructure entry
            smartTable.Load();
            INgpDataRow smartTableInput2 = new NgpDataRow();
            smartTableInput2.TryAdd("EntityType", "CustomAutomationConfigurationStructure");
            smartTableInput2.TryAdd("Operation", "Create");
            smartTableInput2.TryAdd("NameGeneratorName", "CustomAutomationConfigurationStructureNameGenerator");

            var resolvedSmartTable2 = smartTable.Resolve(smartTableInput2, true);
            if (resolvedSmartTable2 != null && resolvedSmartTable2.Tables.Any())
            {
                DataRow row = NgpDataSet.ToDataSet(resolvedSmartTable2).Tables[0].Rows[0];

                if (!string.IsNullOrWhiteSpace(row["EntityType"]?.ToString()) && row["EntityType"].ToString() == "CustomAutomationConfigurationStructure" &&
                    !string.IsNullOrWhiteSpace(row["Operation"]?.ToString()) && row["Operation"].ToString() == "Create" &&
                    !string.IsNullOrWhiteSpace(row["NameGeneratorName"]?.ToString()) && row["NameGeneratorName"].ToString() == "CustomAutomationConfigurationStructureNameGenerator" &&
                    string.IsNullOrWhiteSpace(row["RevisionGeneratorName"]?.ToString()) &&
                    string.IsNullOrWhiteSpace(row["EntityTypeType"]?.ToString()))
                {
                    _tableOrchestration.RemoveSmartTableRows(new Foundation.BusinessOrchestration.TableManagement.InputObjects.RemoveSmartTableRowsInput()
                    {
                        SmartTable = smartTable,
                        Table = resolvedSmartTable2,
                        IgnoreLastServiceId = true,
                    });
                }
            }

            //---End DEE Code---
            return Input;
        }

        public override bool DeeTestCondition(Dictionary<string, object> Input)
        {
            //---Start DEE Condition Code---

            return true;

            //---End DEE Condition Code---
        }
    }
}
