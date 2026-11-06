using Cmf.Foundation.BusinessOrchestration;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Cmf.Custom.Common.SD.Utilities.Extensions;

public static class DeeInputExtensions
{
    /// <summary>Gets a typed item from a DEE input dictionary, or the supplied default value.</summary>
    /// <typeparam name="T">The expected value type.</typeparam>
    /// <param name="Input">The DEE input dictionary.</param>
    /// <param name="inputEntryName">The input entry name.</param>
    /// <param name="defaultValue">The value returned when the entry is missing or has another type.</param>
    /// <returns>The typed entry or <paramref name="defaultValue"/>.</returns>
    public static T GetInputItem<T>(this Dictionary<string, object> Input, string inputEntryName, T defaultValue = default(T))
    {
        T returnObject = defaultValue;
        object oExtractedItem;
        if (Input.TryGetValue(inputEntryName, out oExtractedItem) && oExtractedItem is T)
        {
            returnObject = (T)oExtractedItem;
        }
        return returnObject;
    }

    /// <summary>Gets the service input object contained in a DEE input dictionary.</summary>
    /// <param name="deeInput">The DEE input dictionary.</param>
    /// <returns>The service input, or null when none is present.</returns>
    public static BaseInput GetServiceInputObject(this Dictionary<string, object> deeInput)
    {
        BaseInput serviceInput = null;
        foreach (KeyValuePair<string, object> param in deeInput)
        {
            if (param.Value != null && param.Value is BaseInput)
            {
                serviceInput = param.Value as BaseInput;
            }
        }
        return serviceInput;
    }

    /// <summary>Gets the service output object contained in a DEE input dictionary.</summary>
    /// <param name="deeInput">The DEE input dictionary.</param>
    /// <returns>The service output, or null when none is present.</returns>
    public static BaseOutput GetServiceOutputObject(this Dictionary<string, object> deeInput)
    {
        BaseOutput serviceOutput = null;
        foreach (KeyValuePair<string, object> param in deeInput)
        {
            if (param.Value != null && param.Value is BaseOutput)
            {
                serviceOutput = param.Value as BaseOutput;
            }
        }
        return serviceOutput;
    }

    /// <summary>Merges output messages from another service output into this output.</summary>
    /// <param name="serviceOutput">The output to update.</param>
    /// <param name="newServiceOutput">The output whose messages will be appended.</param>
    /// <param name="additionalMessage">An optional additional feedback message.</param>
    public static void MergeServiceOutputMessages(this BaseOutput serviceOutput, BaseOutput newServiceOutput, FeedbackMessage additionalMessage = null)
    {
        if (serviceOutput != null && newServiceOutput != null)
        {
            if (newServiceOutput.Message != null || serviceOutput.Message != null)
            {
                serviceOutput.Message = (serviceOutput.Message ?? "") + System.Environment.NewLine + (newServiceOutput.Message ?? "");
            }

            if (serviceOutput.FeedbackMessages == null)
                serviceOutput.FeedbackMessages = new Collection<FeedbackMessage>();

            if (newServiceOutput.FeedbackMessages != null)
            {
                foreach (FeedbackMessage msg in newServiceOutput.FeedbackMessages)
                {
                    serviceOutput.FeedbackMessages.Add(msg);
                }
            }

            if (additionalMessage != null)
            {
                serviceOutput.FeedbackMessages.Add(additionalMessage);
            }
        }
    }

    /// <summary>Appends a comment to the original service comments.</summary>
    /// <param name="serviceInput">The original service input.</param>
    /// <param name="comment">The additional comment.</param>
    /// <returns>The combined service comments.</returns>
    public static string GetAppendedServiceComment(this BaseInput serviceInput, string comment)
    {
        if (serviceInput != null && !string.IsNullOrWhiteSpace(serviceInput.ServiceComments))
        {
            comment = serviceInput.ServiceComments + System.Environment.NewLine + (comment ?? "");
        }
        return comment;
    }
}
