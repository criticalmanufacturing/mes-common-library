using Cmf.Foundation.BusinessObjects.Abstractions;
using Cmf.Foundation.Common.Abstractions;
using System.Collections;
using System.Collections.Generic;

namespace Cmf.Community.IoTCustomAutomationConfiguration.Actions.Common.BusinessObjects.Abstractions
{
    public interface ICustomAutomationConfigurationCollection : IBusinessEntityInstanceCollection<ICustomAutomationConfiguration>, IEntityInstanceCollection<ICustomAutomationConfiguration>, IEntityCollection<ICustomAutomationConfiguration>, IEntityBaseCollection<ICustomAutomationConfiguration>, ICoreBaseCollection<ICustomAutomationConfiguration>, ICoreBaseCollection, IList<ICustomAutomationConfiguration>, ICollection<ICustomAutomationConfiguration>, IEnumerable<ICustomAutomationConfiguration>, IEnumerable
    {

    }
}