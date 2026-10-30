using Cmf.Foundation.BusinessObjects.Abstractions;
using Cmf.Foundation.Common.Abstractions;
using System;
using System.Collections.ObjectModel;

namespace Cmf.Community.IoTCustomAutomationConfiguration.Actions.Common.BusinessObjects.Abstractions
{
    public interface ICustomAutomationConfiguration : IBusinessEntityInstance, IEntityInstance, IEntity, IEntityBase, ICoreBase, INamedEntity
    {
        IAutomationControllerInstance AutomationControllerInstance
        {
            get;
            set;
        }

        IAutomationDriverInstance AutomationDriverInstance
        {
            get;
            set;
        }

        ICustomAutomationConfiguration CreateInstance(string name);

        ICustomAutomationConfiguration CreateInstance(string name, IOperationAttributeCollection operationAttributeCollection);

        ICustomAutomationConfigurationCollection CreateInstances(Collection<string> instanceNames);

        ICustomAutomationConfigurationCollection CreateInstances(Collection<string> instanceNames, IOperationAttributeCollection operationAttributeCollection);

        ICustomAutomationConfiguration CreateTemplateInstance(string name);

        ICustomAutomationConfiguration CreateTemplateInstance(string name, IOperationAttributeCollection operationAttributeCollection);
    }
}