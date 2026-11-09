using Cmf.Foundation.BusinessObjects.Abstractions;
using Cmf.Foundation.Common.Abstractions;
using Cmf.Navigo.BusinessObjects.Abstractions;
using System;

namespace Cmf.Community.IoTCustomAutomationConfiguration.Actions.Common.BusinessObjects.Abstractions
{
    public interface ICustomAutomationConfigurationEntity : IBusinessEntityRelation, IEntityRelation, IEntityBase, ICoreBase, INamedEntity
    {
        IResource SourceEntity
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