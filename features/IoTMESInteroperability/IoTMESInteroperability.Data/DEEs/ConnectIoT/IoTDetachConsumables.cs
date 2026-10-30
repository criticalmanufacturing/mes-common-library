using System;
using System.Collections.Generic;
using System.Linq;
using Cmf.Navigo.BusinessObjects;
using Cmf.Common.CustomActionUtilities;
using Cmf.Community.IoTMESInteroperability.Common;
using Cmf.Navigo.BusinessObjects.Abstractions;
using Cmf.Navigo.BusinessOrchestration.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Cmf.Foundation.Common.Abstractions;
using Cmf.Navigo.BusinessOrchestration.MaterialManagement;
using System.Collections;

namespace Cmf.Community.IoTMESInteroperability.Actions
{
    public class IoTDetachConsumables : DeeDevBase
    {
        public override bool DeeTestCondition(Dictionary<string, object> Input)
        {
            //---Start DEE Condition Code---

            #region Info

            /// <summary>
            /// Summary text
            ///     DEE To be Invoked by IoT to Perform an Operation
            /// Action Groups: 
            /// Depends On:
            /// Is Dependency For:
            /// Exceptions:
            /// </summary>

            #endregion Info

            bool isToExecute = true;

            return isToExecute;

            //---End DEE Condition Code---

        }

        public override Dictionary<string, object> DeeActionCode(Dictionary<string, object> Input)
        {
            //---Start DEE Code---

            // CORE
            UseReference("Cmf.Foundation.Common.dll", "Cmf.Foundation.Common.Abstractions");

            // MES
            UseReference("Cmf.Navigo.Common.dll", "Cmf.Navigo.Common");
            UseReference("Cmf.Navigo.BusinessObjects.dll", "Cmf.Navigo.BusinessObjects");
            UseReference("Cmf.Navigo.BusinessOrchestration.dll", "Cmf.Navigo.BusinessOrchestration.Abstractions");

            // Common
            UseReference("Cmf.Common.CustomActionUtilities.dll", "Cmf.Common.CustomActionUtilities");

            // Custom            
            UseReference("Cmf.Community.IoTMESInteroperability.Common.dll", "Cmf.Community.IoTMESInteroperability.Common");
            UseReference("Cmf.Community.IoTMESInteroperability.Common.dll", "Cmf.Community.IoTMESInteroperability");


            #region Service Provider
            // Get services provider information

            IServiceProvider serviceProvider = (IServiceProvider)Input["ServiceProvider"];

            IEntityFactory entityFactory = serviceProvider.GetService<IEntityFactory>();

            // IMaterialOrchestration _materialOrchestration = serviceProvider.GetService<IMaterialOrchestration>();
            // IResourceOrchestration _resourceOrchestration = serviceProvider.GetService<IResourceOrchestration>();
            #endregion

            string[] reelNames = ((IEnumerable)Input["ReelNames"]).Cast<object>()
                                .Select(x => x.ToString())
                                .ToArray();

            foreach (string reelName in reelNames)
            {
                IMaterial material = entityFactory.Create<IMaterial>();
                material.Name = reelName;
                material.Load(); // Missing mandatory property 'Id' for type Material.
                material.LoadRelations("MaterialResource");
                IResource resource = material.MaterialResourceRelations.FirstOrDefault().TargetEntity;
                if (resource != null)
                {
                    resource.Load(); // With multiple reels, might get several operations on same resource.
                    resource.DetachConsumable(material);
                }
            }

            //---End DEE Code---
            return Input;
        }
    }
}
