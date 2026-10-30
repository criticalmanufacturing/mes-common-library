using System;
using System.Collections.Generic;
using Cmf.Navigo.BusinessOrchestration.MaterialManagement.InputObjects;
using Cmf.Navigo.BusinessOrchestration.Abstractions;
using Microsoft.Extensions.DependencyInjection;


namespace Cmf.Community.IoTMESInteroperability.Actions
{
    public class IoTTerminateMaterial : DeeDevBase
    {
        public override bool DeeTestCondition(Dictionary<string, object> Input)
        {
            //---Start DEE Condition Code---

            #region Info

            /// <summary>
            /// Summary text
            ///     Action to terminate a Material
            /// Action Groups:
            /// </summary>
            /// 
            #endregion

            return true;

            //---End DEE Condition Code---

        }

        public override Dictionary<string, object> DeeActionCode(Dictionary<string, object> Input)
        {
            //---Start DEE Code---

            UseReference("Cmf.Foundation.Common.dll", "Cmf.Foundation.Common.Abstractions");
            UseReference("Cmf.Navigo.BusinessOrchestration.dll", "Cmf.Navigo.BusinessOrchestration.Abstractions");
            UseReference("Cmf.Navigo.BusinessOrchestration.dll", "Cmf.Navigo.BusinessOrchestration.MaterialManagement.InputObjects");

            // Get services provider information
            IServiceProvider serviceProvider = (IServiceProvider)Input["ServiceProvider"];

            if (Input.ContainsKey("TerminateMaterialInput") && Input["TerminateMaterialInput"] != null)
            {
                TerminateMaterialInput input = Input["TerminateMaterialInput"] as TerminateMaterialInput;
                input.Material.Load();
                input.LossReason.Load();

                IMaterialOrchestration _materialOrchestration = serviceProvider.GetService<IMaterialOrchestration>();
                _materialOrchestration.TerminateMaterial(input);
            }
            return Input;
            //---End DEE Code---
        }
    }
}
