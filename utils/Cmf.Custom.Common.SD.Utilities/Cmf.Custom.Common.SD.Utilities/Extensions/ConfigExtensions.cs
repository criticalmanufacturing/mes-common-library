using Cmf.Foundation.Common;
using Cmf.Foundation.Configuration.Abstractions;
using System.Security;

namespace Cmf.Custom.Common.SD.Utilities.Extensions;

public static class ConfigExtensions
{
    /// <summary>
    /// Gets the configuration value of type T from the IConfig object.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="config"></param>
    /// <param name="throwExceptionIfNotFound"></param>
    /// <returns></returns>
    /// <exception cref="ArgumentNullException"></exception>
    /// <exception cref="InvalidOperationException"></exception>
    public static T GetConfigurationValue<T>(this IConfig config, bool throwExceptionIfNotFound = true)
    {
        if (config == null)
        {
            throw new ArgumentNullException(nameof(config), "Configuration object cannot be null.");
        }

        if (config.Value is not T typedValue)
        {
            if (throwExceptionIfNotFound)
            {
                throw new InvalidOperationException($"Configuration value of type {typeof(T).Name} not found.");
            }
            else
            {
                return default!;
            }
        }

        return typedValue;
    }

    /// <summary>
    ///     Gets the descrypted config value
    /// </summary>
    /// <param name="configPath">
    ///     The config path.
    /// </param>
    /// <param name="throwExceptionIfEmpty">
    ///     If true, throw exception if empty.
    /// </param>
    /// <returns>
    ///     Config value.
    /// </returns>
    /// <exception cref="InvalidObjectTypeCmfException">
    ///     If the config is not of type SecureString
    /// </exception>
    /// <exception cref="MissingMandatoryConfigCmfException">
    ///     If the config is empty.
    /// </exception>
    public static string GetDecryptedConfigValue(this IConfig config, string configPath, bool throwExceptionIfEmpty = true)
    {
        // Exception will be thrown if config does not exist
        config.Load(configPath);

        if (config.ValueType != typeof(SecureString).FullName)
        {
            throw new InvalidObjectTypeCmfException(configPath, config.ValueType, typeof(SecureString).FullName);
        }

        string value = config.Value as string;

        if (throwExceptionIfEmpty && (value == null || string.IsNullOrEmpty(value)))
        {
            throw new MissingMandatoryConfigCmfException(configPath);
        }

        return config.GetDecryptedConfigValue();
    }
}