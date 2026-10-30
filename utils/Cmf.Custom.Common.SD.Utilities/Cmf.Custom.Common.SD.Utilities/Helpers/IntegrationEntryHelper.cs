using Cmf.Custom.Common.SD.Utilities.Extensions;
using Cmf.Custom.Common.SD.Utilities.Helpers.Abstractions;
using Cmf.Foundation.BusinessObjects.Abstractions;
using Cmf.Foundation.BusinessOrchestration.Abstractions;
using Cmf.Foundation.BusinessOrchestration.GenericServiceManagement.InputObjects;
using Cmf.Foundation.Common;
using Cmf.Foundation.Common.Abstractions;
using System.Data;

namespace Cmf.Custom.Common.SD.Utilities.Helpers;

/// <summary>
///     Employee Helpers
/// </summary>
/// <remarks>
///     Initializes a new instance of the <see cref="IntegrationEntryHelper"/> class.
/// </remarks>
/// <param name="entityFactory">
///     The entity factory.
/// </param>
/// <param name="genericServiceOrchestration">
///     The generic service orchestration.
/// </param>
public class IntegrationEntryHelper(IEntityFactory entityFactory, IGenericServiceOrchestration genericServiceOrchestration) : IIntegrationEntryHelpers
{
    private readonly IEntityFactory _entityFactory = entityFactory;
    private readonly IGenericServiceOrchestration _genericServiceOrchestration = genericServiceOrchestration;

    /// <inheritdoc/>
    public IIntegrationEntry CreateIntegrationEntry(string name, string sourceSystem, string targetSystem, string messageType, string eventName, object dataObject, IIntegrationEntry parent = null, bool isToCreate = true)
    {
        IIntegrationEntry ie = _entityFactory.Create<IIntegrationEntry>();
        ie.IsRetriable = true;
        ie.NumberOfRetries = 0;
        ie.Name = name;
        ie.EventName = eventName;
        ie.SourceSystem = sourceSystem;
        ie.TargetSystem = targetSystem;
        ie.MessageType = messageType;
        ie.IntegrationMessage = _entityFactory.Create<IIntegrationMessage>();
        ie.IntegrationMessage.Message = dataObject.ToJsonBytes();
        ie.SystemState = IntegrationEntrySystemState.Received;
        ie.MessageDate = DateTime.Now;
        ie.ParentIntegrationEntry = parent;

        if (isToCreate)
        {
            _genericServiceOrchestration.CreateObject(new CreateObjectInput() { Object = ie });
        }

        return ie;
    }
}
