using Cmf.Foundation.BusinessObjects.Abstractions;
using Cmf.Foundation.Common.Abstractions;
using System;

namespace Cmf.Community.IoTCustomAutomationConfiguration.Actions.Common.BusinessObjects.Abstractions
{
    public interface ICustomAutomationConfigurationStructure : IBusinessEntityRelation, IEntityRelation, IEntityBase, ICoreBase, INamedEntity
    {
        ICustomAutomationConfiguration SourceEntity
        {
            get;
            set;
        }

        ICustomAutomationConfiguration TargetEntity
        {
            get;
            set;
        }
    }
}