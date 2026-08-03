using Cmf.Foundation.BusinessObjects.Abstractions;
using Cmf.Foundation.Common.Abstractions;
using Cmf.Navigo.BusinessObjects.Abstractions;
using System;

namespace Cmf.Community.IoTCustomAutomationConfiguration.Actions.Common.BusinessObjects.Abstractions
{
    public interface ICustomAutomationConfigurationValue : IBusinessEntityRelation, IEntityRelation, IEntityBase, ICoreBase, INamedEntity
    {
        ICustomAutomationConfiguration SourceEntity
        {
            get;
            set;
        }

        IParameter TargetEntity
        {
            get;
            set;
        }

        string Value
        {
            get;
            set;
        }
    }
}