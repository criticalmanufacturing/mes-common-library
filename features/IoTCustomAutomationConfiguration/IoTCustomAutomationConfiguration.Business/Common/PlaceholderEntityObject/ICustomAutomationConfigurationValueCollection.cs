using Cmf.Foundation.BusinessObjects.Abstractions;
using Cmf.Foundation.Common.Abstractions;
using System.Collections;
using System.Collections.Generic;

namespace Cmf.Community.IoTCustomAutomationConfiguration.Actions.Common.BusinessObjects.Abstractions
{
    public interface ICustomAutomationConfigurationValueCollection : IBusinessEntityRelationCollection<ICustomAutomationConfigurationValue>, IEntityRelationCollection<ICustomAutomationConfigurationValue>, IEntityBaseCollection<ICustomAutomationConfigurationValue>, ICoreBaseCollection<ICustomAutomationConfigurationValue>, ICoreBaseCollection, IList<ICustomAutomationConfigurationValue>, ICollection<ICustomAutomationConfigurationValue>, IEnumerable<ICustomAutomationConfigurationValue>, IEnumerable
    {

    }
}