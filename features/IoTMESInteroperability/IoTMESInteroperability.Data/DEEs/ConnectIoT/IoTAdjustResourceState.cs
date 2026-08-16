using System;
using System.Collections.Generic;
using Cmf.Foundation.Common.Abstractions;
using Cmf.Navigo.BusinessObjects.Abstractions;
using Cmf.Navigo.BusinessOrchestration.Abstractions;
using Cmf.Navigo.BusinessOrchestration.ResourceManagement.InputObjects;
using Microsoft.Extensions.DependencyInjection;

namespace Cmf.Community.IoTMESInteroperability.Actions
{
    public class IoTAdjustResourceState : DeeDevBase
    {

        public override bool DeeTestCondition(Dictionary<string, object> Input)
        {
            //---Start DEE Condition Code---

            #region Info

            /// <summary>
            /// Summary text
            ///     Custom DEE Action to update the resource state
            /// Action Groups:
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

            // MES
            UseReference("Cmf.Navigo.BusinessOrchestration.dll", "Cmf.Navigo.BusinessOrchestration.Abstractions");
            UseReference("Cmf.Navigo.BusinessOrchestration.dll", "Cmf.Navigo.BusinessOrchestration.ResourceManagement.InputObjects");

            //Please start code here

            var serviceProvider = (IServiceProvider)Input["ServiceProvider"];
            IEntityFactory _entityFactory = serviceProvider.GetService<IEntityFactory>();

            if (Input.ContainsKey("ResourceName") &&
                Input["ResourceName"] != null &&
                Input.ContainsKey("StateModelStateName") &&
                Input["StateModelStateName"] != null &&
                !string.IsNullOrWhiteSpace(Input["StateModelStateName"].ToString()))
            {
                IResource _resource = _entityFactory.Create<IResource>();
                _resource.Name = Input["ResourceName"].ToString();
                Input["ResourceNameOut"] = _resource.Name;
                _resource.Load();

                string stateName = Input["StateModelStateName"].ToString();

                if (!_resource.CurrentMainState.StateModel.Name.Equals(stateName))
                {
                    IResourceOrchestration _resourceOrchestration = serviceProvider.GetService<IResourceOrchestration>();

                    AdjustResourceStateInput _adjustResourceStateInput = new AdjustResourceStateInput()
                    {
                        Resource = _resource,
                        StateModel = _resource.CurrentMainState.StateModel,
                        StateModelStateName = stateName,
                        IgnoreLastServiceId = true
                    };

                    _resourceOrchestration.AdjustResourceState(_adjustResourceStateInput);
                }
            }

            //---End DEE Code---

            return Input;

        }
    }
}
