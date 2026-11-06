using Cmf.Foundation.Common;
using Cmf.Foundation.Configuration;
using System.Linq;
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

    /// <summary>Finds a child configuration whose path contains the supplied path fragment.</summary>
    /// <param name="parentConfig">The parent configuration.</param>
    /// <param name="path">The path fragment to find.</param>
    /// <param name="loadChilds">Whether to load child configurations first.</param>
    /// <returns>The matching child configuration.</returns>
    /// <exception cref="CmfBaseException">Thrown when no child configuration matches.</exception>
    public static IConfig GetChildConfigByPath(this IConfig parentConfig, string path, bool loadChilds = false)
    {
        IConfig childConfig = null;
        if (loadChilds)
        {
            parentConfig.LoadChildConfigs();
        }
        if (parentConfig.ChildConfigs != null && parentConfig.ChildConfigs.Any())
        {
            childConfig = parentConfig.ChildConfigs.FirstOrDefault(x => x.Path.Contains(path));
        }
        if (childConfig == null)
        {
            throw new Cmf.Foundation.Common.CmfBaseException(string.Format("Specified child config does not exist path: {0}", path));
        }
        return childConfig;
    }

    /// <summary>Gets a configuration value by path.</summary>
    /// <typeparam name="T">The value type.</typeparam>
    /// <param name="configPath">The configuration path.</param>
    /// <param name="throwExceptionIfEmpty">Preserved for source compatibility.</param>
    /// <returns>The configuration value.</returns>
    /// <exception cref="CmfBaseException">Thrown when the configuration is missing or empty.</exception>
    public static T GetConfigValue<T>(string configPath, bool throwExceptionIfEmpty = false)
    {
        Cmf.Foundation.Configuration.Config.TryGetConfig(configPath, out IConfig config);
        object returnValue;
        if (config != null && config.Value != null)
        {
            returnValue = config.Value;
        }
        else
        {
            throw new Cmf.Foundation.Common.CmfBaseException(string.Format("Specified config does not exist path: {0}", configPath));
        }
        return (T)returnValue;
    }

}