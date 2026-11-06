using System.Text;
using Cmf.Foundation.Configuration.Abstractions;

namespace Cmf.Custom.Common.SD.Utilities.Extensions;

public static class StringExtensions
{
    /// <summary>
    /// Validates if the config value is a valid list of strings separated by a separator.
    /// </summary>
    /// <returns>True if valid, otherwise False</returns>
    public static bool IsStringListValid(this string configValue, string separator)
    {
        if (string.IsNullOrWhiteSpace(configValue) || string.IsNullOrWhiteSpace(separator))
        {
            return false;
        }

        if (configValue.StartsWith(separator) || configValue.EndsWith(separator))
        {
            return false;
        }

        string[] parts = configValue.Split(separator);
        return parts.All(p => !string.IsNullOrWhiteSpace(p));
    }

    /// <summary>
    /// Converts the input string into SHA256 hash
    /// </summary>
    /// <returns>Hashed value</returns>
    public static string CreateHash(this string valueToHash)
    {
        byte[] inputBytes = Encoding.ASCII.GetBytes(valueToHash);
        byte[] hashBytes = System.Security.Cryptography.MD5.HashData(inputBytes);

        return Convert.ToHexString(hashBytes);
    }
}