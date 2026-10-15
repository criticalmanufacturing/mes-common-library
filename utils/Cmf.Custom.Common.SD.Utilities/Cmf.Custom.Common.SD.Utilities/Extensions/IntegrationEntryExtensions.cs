using Cmf.Foundation.BusinessObjects.Abstractions;
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
}