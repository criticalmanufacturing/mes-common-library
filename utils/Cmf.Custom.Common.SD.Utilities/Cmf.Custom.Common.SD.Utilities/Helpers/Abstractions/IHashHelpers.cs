namespace Cmf.Custom.Common.SD.Utilities.Helpers.Abstractions;

/// <summary>
///     Hash Helpers
/// </summary>
public interface IHashHelpers
{
    /// <summary>
    ///     Creates a hash for the specified value.
    /// </summary>
    /// <param name="valueToHash">
    ///     The value to hash.
    /// </param>
    /// <returns>
    ///     The hashed value.
    /// </returns>
    public string CreateHash(string valueToHash);
}