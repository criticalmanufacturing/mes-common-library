using Cmf.Common.CustomActionUtilities.Abstractions;
using Cmf.Foundation.Common;
using Microsoft.Extensions.DependencyInjection;

namespace Cmf.Community.IoTMESInteroperability.Common
{
    /// <summary>
    /// Thin static facade over <see cref="IGenericHelper"/>, so DEEs and orchestrations that don't
    /// receive it through DI can still throw/localize messages and read configs with a single call.
    /// </summary>
    public static class GeneralUtilities
    {
        private static IGenericHelper GenericHelper => ApplicationContext.CurrentServiceProvider.GetService<IGenericHelper>();

        /// <summary>
        /// Builds the localized message for <paramref name="key"/> and throws it as an exception.
        /// </summary>
        public static void ThrowLocalizedException(string key, params string[] parameters)
        {
            GenericHelper.ThrowLocalizedException(key, parameters);
        }

        /// <summary>
        /// Resolves the localized message for <paramref name="key"/> without throwing.
        /// </summary>
        public static string GetLocalizedMessage(string key, params object[] parameters)
        {
            return GenericHelper.GetLocalizedMessage(key, parameters);
        }

        /// <summary>
        /// Reads a configuration value from the given path.
        /// </summary>
        public static T GetConfigValue<T>(string configPath, bool throwExceptionIfEmpty = true)
        {
            return GenericHelper.GetConfigValue<T>(configPath, throwExceptionIfEmpty);
        }
    }
}
