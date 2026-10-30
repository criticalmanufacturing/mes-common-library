using Cmf.Foundation.BusinessObjects;
using Cmf.Foundation.BusinessObjects.Abstractions;
using Cmf.Foundation.Common;
using Cmf.Foundation.Common.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;

namespace Cmf.Community.IoTCustomAutomationConfiguration.Actions.Actions.NameGenerator
{
    public class CustomAutomationConfigurationNameGenerator : DeeDevBase
    {
        public override Dictionary<string, object> DeeActionCode(Dictionary<string, object> Input)
        {
            //---Start DEE Code---

            //System
            UseReference("", "System");
            UseReference("", "System.Collections.Generic");

            //Navigo
            UseReference("Cmf.Foundation.BusinessObjects.dll", "Cmf.Foundation.BusinessObjects.Abstractions");
            UseReference("Cmf.Foundation.BusinessObjects.dll", "Cmf.Foundation.BusinessObjects");
            UseReference("Cmf.Foundation.Common.dll", "Cmf.Foundation.Common.Abstractions");
            UseReference("Cmf.Foundation.Common.dll", "Cmf.Foundation.Common");

            var serviceProvider = (IServiceProvider)Input["ServiceProvider"];
            IEntityFactory entityFactory = serviceProvider.GetService<IEntityFactory>();

            dynamic entity = (Input["EntitySource"]) as dynamic;

            AutomationControllerInstance controllerInstance = entity.AutomationControllerInstance;
            controllerInstance.Load();

            IEntity driverInstanceRelatedEntity = null;
            AutomationDriverInstance driver = entity.AutomationDriverInstance;
            if (driver != null)
            {
                driver.Load();
                Type driverRelatedEntityType = driver.ObjectType.EntityTypeInterface;
                driverInstanceRelatedEntity = entityFactory.Create(driverRelatedEntityType) as IEntity;
                driverInstanceRelatedEntity.Load((driver.ObjectId as long?).Value);
            }

            string name = string.Empty;
            Type controllerRelatedEntityType = controllerInstance.ObjectType.EntityTypeInterface;

            IEntity controllerInstanceRelatedEntity = entityFactory.Create(controllerRelatedEntityType) as IEntity;
            controllerInstanceRelatedEntity.Load((controllerInstance.ObjectId as long?).Value);

            if (driverInstanceRelatedEntity != null)
            {
                var controllerDriverDefinition = driver.AutomationControllerDriverDefinition;
                controllerDriverDefinition.Load();
                var controllerDefintion = ApplicationContext.CallContext.GetInformationContext("IsControllerDefintion") as bool?;
                name = $"ControllerInstance_{controllerInstanceRelatedEntity.Name}_Driver_{controllerDriverDefinition.Name}_{driverInstanceRelatedEntity.Name}_{(controllerDefintion.HasValue && controllerDefintion.Value ? "Controller" : "Driver")}";
            }
            else
            {
                name = $"ControllerInstance_{controllerInstanceRelatedEntity.Name}";
            }

            Input["Result"] = $"{name}_{DateTime.UtcNow.Ticks}";
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
