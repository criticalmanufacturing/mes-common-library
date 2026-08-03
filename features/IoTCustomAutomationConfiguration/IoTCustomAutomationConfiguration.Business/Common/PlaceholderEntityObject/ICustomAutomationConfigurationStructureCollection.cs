using Cmf.Foundation.BusinessObjects.Abstractions;
using Cmf.Foundation.Common.Abstractions;
using System.Collections;
using System.Collections.Generic;

namespace Cmf.Community.IoTCustomAutomationConfiguration.Actions.Common.BusinessObjects.Abstractions
{
    public interface ICustomAutomationConfigurationStructureCollection : IBusinessEntityRelationCollection<ICustomAutomationConfigurationStructure>, IEntityRelationCollection<ICustomAutomationConfigurationStructure>, IEntityBaseCollection<ICustomAutomationConfigurationStructure>, ICoreBaseCollection<ICustomAutomationConfigurationStructure>, ICoreBaseCollection, IList<ICustomAutomationConfigurationStructure>, ICollection<ICustomAutomationConfigurationStructure>, IEnumerable<ICustomAutomationConfigurationStructure>, IEnumerable
    {

    }
}