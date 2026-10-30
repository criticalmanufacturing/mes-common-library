using Cmf.Foundation.BusinessObjects.Abstractions;

namespace Cmf.Custom.Common.SD.Utilities.Helpers.Abstractions;

/// <summary>
///     Integration Entry Helpers
/// </summary>
public interface IIntegrationEntryHelpers
{
    /// <summary>
    ///     Creates an <see cref="IIntegrationEntry"/>.
    /// </summary>
    /// <param name="name">
    ///     The integration entry name.
    /// </param>
    /// <param name="sourceSystem">
    ///     The source system.
    /// </param>
    /// <param name="targetSystem">
    ///     The target system.
    /// </param>
    /// <param name="messageType">
    ///     The message type.
    /// </param>
    /// <param name="eventName">
    ///     The event name.
    /// </param>
    /// <param name="dataObject">
    ///     The data object to serialize into the integration message.
    /// </param>
    /// <param name="parent">
    ///     The parent integration entry.
    /// </param>
    /// <param name="isToCreate">
    ///     Indicates whether the integration entry should be persisted.
    /// </param>
    /// <returns>
    ///     The created <see cref="IIntegrationEntry"/>.
    /// </returns>
    public IIntegrationEntry CreateIntegrationEntry(
        string name,
        string sourceSystem,
        string targetSystem,
        string messageType,
        string eventName,
        object dataObject,
        IIntegrationEntry parent = null,
        bool isToCreate = true);
}