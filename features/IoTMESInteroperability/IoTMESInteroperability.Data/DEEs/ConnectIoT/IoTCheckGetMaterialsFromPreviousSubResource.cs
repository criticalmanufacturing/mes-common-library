using Cmf.Common.CustomActionUtilities;
using Cmf.Navigo.BusinessObjects;
using System;
using System.Collections.Generic;
using System.Linq;
using Cmf.Navigo.BusinessObjects.Abstractions;
using Cmf.Foundation.BusinessOrchestration.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Cmf.Foundation.Common.Abstractions;
using Cmf.Community.IoTMESInteroperability.Utilities;

namespace Cmf.Community.IoTMESInteroperability.Actions
{
    public class IoTCheckGetMaterialsFromPreviousSubResource : DeeDevBase
    {
        public override bool DeeTestCondition(Dictionary<string, object> Input)
        {
            //---Start DEE Condition Code---

            #region Info

            /// <summary>
            /// Summary text
            ///     DEE To be invoked by IoT to CheckGetMaterialsFromPreviousSubResource
            /// Action Groups:
            /// Depends On:
            /// Is Dependency For:
            /// Exceptions:
            /// </summary>

            #endregion

            bool isToExecute = true;

            return isToExecute;

            //---End DEE Condition Code---

        }

        public override Dictionary<string, object> DeeActionCode(Dictionary<string, object> Input)
        {
            //---Start DEE Code---

            // System
            UseReference("", "System.Data");

            // CORE
            UseReference("Cmf.Foundation.Common.dll", "Cmf.Foundation.Common.Abstractions");

            // MES
            UseReference("Cmf.Navigo.BusinessObjects.dll", "Cmf.Navigo.BusinessObjects");
            UseReference("Cmf.Navigo.BusinessObjects.dll", "Cmf.Navigo.BusinessObjects.Abstractions");

            // Custom
            UseReference("Cmf.Custom.IoT.Utilities.Common.dll", "Cmf.Custom.IoT.Utilities.Common");
            UseReference("Cmf.Custom.IoT.Utilities.Common.dll", "Cmf.Custom.IoT.Utilities");
            UseReference("Cmf.Custom.IoT.Utilities.Common.dll", "Cmf.Custom.IoT.Utilities.Common.Objects");

            // Common
            UseReference("Cmf.Common.CustomActionUtilities.dll", "Cmf.Common.CustomActionUtilities");

            // 3rd Party
            UseReference("Newtonsoft.Json.dll", "Newtonsoft.Json");

            #region Service Provider

            // Get services provider information
            IServiceProvider serviceProvider = (IServiceProvider)Input["ServiceProvider"];
            IEntityFactory entityFactory = serviceProvider.GetService<IEntityFactory>();

            #endregion

            string resourceName = Input["ResourceName"] as string;
            IResource resource = entityFactory.Create<IResource>();
            resource.Name = resourceName;
            resource.Load();

            IMaterialCollection materials = null;
            materials = IoTUtilities.GetMaterialsFromPreviousSubResource(resource);

            Dictionary<string, object> outputDEE = new Dictionary<string, object>();

            outputDEE.Add("MaterialsOutputFirst", materials?.First()?.Name.ToString());

            return (outputDEE);

            //---End DEE Code---
        }
    }
}




