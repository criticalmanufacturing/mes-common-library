using Cmf.Foundation.BusinessObjects.Abstractions;
using Cmf.Custom.Common.SD.Utilities.Exceptions;
using Cmf.Navigo.BusinessObjects;
using System.Diagnostics;
using System.Threading;
using Newtonsoft.Json;

namespace Cmf.Custom.Common.SD.Utilities.Extensions;

public static class IntegrationEntryExtensions
{
    /// <summary>
    ///     Gets the object of type T from the integration entry.
    /// </summary>
    /// <typeparam name="T">
    ///     The type of the object to return.
    /// </typeparam>
    /// <param name="integrationEntry">
    ///     The integration entry from which to retrieve the object.
    /// </param>
    /// <returns>
    ///     The object of type T, or the default value of type T if not found.
    /// </returns>
    public static T GetObjectFromIntegrationEntry<T>(this IIntegrationEntry integrationEntry)
    {
        if (integrationEntry.IntegrationMessage == null)
            return default;

        using (StreamReader sr = new StreamReader(new MemoryStream(integrationEntry.IntegrationMessage.Message)))
        {
            return JsonConvert.DeserializeObject<T>(sr.ReadToEnd());
        }
    }

    /// <summary>
    /// Checks whether the parent integration entry is processed or rejected and throws if it is not.
    /// </summary>
    /// <remarks>
    /// Parent state may not have changed in the database when this entry starts processing. This waits until it is no longer Received or Processing, then verifies the final state.
    /// </remarks>
    /// <param name="integrationEntry">The integration entry whose parent is checked.</param>
    /// <param name="timeout">How long, in milliseconds, to wait for the state to change.</param>
    /// <param name="interval">How long, in milliseconds, to wait between checks.</param>
    /// <exception cref="ParentIntegrationEntryNotProcessedCmfException">The parent is not processed or rejected before the timeout.</exception>
    public static void TryWaitForParentToBeProcessedOrRejected(this IIntegrationEntry integrationEntry, int timeout = 3000, int interval = 400)
    {
        if (integrationEntry.ParentIntegrationEntry != null)
        {
            Stopwatch sw = Stopwatch.StartNew();
            do
            {
                integrationEntry.ParentIntegrationEntry.Load();
                if (integrationEntry.ParentIntegrationEntry.SystemState != IntegrationEntrySystemState.Received
                    && integrationEntry.ParentIntegrationEntry.SystemState != IntegrationEntrySystemState.Processing)
                    break;
                Thread.Sleep(interval);
            } while (sw.Elapsed.TotalMilliseconds < timeout);
            sw.Stop();
            if (integrationEntry.ParentIntegrationEntry.SystemState != IntegrationEntrySystemState.Processed
                && integrationEntry.ParentIntegrationEntry.SystemState != IntegrationEntrySystemState.Rejected)
                throw new ParentIntegrationEntryNotProcessedCmfException(integrationEntry.ParentIntegrationEntry.Name);
        }
    }

}