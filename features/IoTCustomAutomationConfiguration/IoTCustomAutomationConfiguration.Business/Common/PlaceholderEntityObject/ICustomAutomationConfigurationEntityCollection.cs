using Cmf.Foundation.BusinessObjects.Abstractions;
using Cmf.Foundation.Common.Abstractions;
using System.Collections;
using System.Collections.Generic;

namespace Cmf.Community.IoTCustomAutomationConfiguration.Actions.Common.BusinessObjects.Abstractions
{
    public interface ICustomAutomationConfigurationEntityCollection : IBusinessEntityRelationCollection<ICustomAutomationConfigurationEntity>, IEntityRelationCollection<ICustomAutomationConfigurationEntity>, IEntityBaseCollection<ICustomAutomationConfigurationEntity>, ICoreBaseCollection<ICustomAutomationConfigurationEntity>, ICoreBaseCollection, IList<ICustomAutomationConfigurationEntity>, ICollection<ICustomAutomationConfigurationEntity>, IEnumerable<ICustomAutomationConfigurationEntity>, IEnumerable
    {

    }
}