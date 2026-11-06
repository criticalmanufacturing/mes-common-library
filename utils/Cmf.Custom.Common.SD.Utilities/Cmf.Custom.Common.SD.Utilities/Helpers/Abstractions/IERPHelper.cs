using Cmf.Custom.Common.SD.Utilities.Exceptions;
using Cmf.Foundation.BusinessObjects.Abstractions;

namespace Cmf.Custom.Common.SD.Utilities.Helpers.Abstractions
{
    /// <summary>
    /// This is a utility class that can be helpful when using or manipulating ERPs.
    /// </summary>
    public interface IERPHelper
    {
        /// <summary>
        /// This method creates an Integration Entry and then returns it.
        /// </summary>
        /// <param name="sourceSystem">The integration entry's source system.</param>
        /// <param name="targetSystem">The integration entry's target system.</param>
        /// <param name="messageType">The integration entry's message type.</param>
        /// <param name="eventName">The integration entry's event name.</param>
        /// <param name="messageName">The integration entry's message name.</param>
        /// <param name="message">The integration entry's general message (before being encoded).</param>
        /// <param name="batchId">The integration entry's batchId (that is null by default).</param>
        /// <param name="name">The integration entry's name (that is null by default), If null then a new one will be generated.</param>
        /// <param name="isBatchIdQueueingEnable">Indicates if the new IE should be set as children of the most recently created IE with the same <paramref name="batchId"/>, forcing MES to process them as a queue and never in parallel.</param>
        /// <returns>The created Integration Entry.</returns>
        IIntegrationEntry CreateIntegrationEntry(string sourceSystem, string targetSystem, string messageType, string eventName, string messageName, string message, string batchId = null, string name = null, bool isBatchIdQueueingEnable = false);


    }
}
