using Cmf.Custom.Common.SD.Utilities.Extensions;
using Cmf.Custom.Common.SD.Utilities.Helpers.Abstractions;
using Cmf.Foundation.BusinessObjects.Abstractions;
using Cmf.Foundation.BusinessOrchestration.Abstractions;
using Cmf.Foundation.BusinessOrchestration.GenericServiceManagement.InputObjects;
using Cmf.Foundation.Common;
using Cmf.Foundation.Common.Abstractions;
using System.Collections.ObjectModel;
using System.Data;
using System.Text;

namespace Cmf.Custom.Common.SD.Utilities.Helpers;

/// <summary>
///     Hash Helpers
/// </summary>
/// <remarks>
///     Initializes a new instance of the <see cref="HashHelper"/> class.
/// </remarks>
public class HashHelper() : IHashHelpers
{
    /// <inheritdoc/>
    public string CreateHash(string valueToHash)
    {
        using System.Security.Cryptography.MD5 md5 = System.Security.Cryptography.MD5.Create();
        byte[] inputBytes = Encoding.ASCII.GetBytes(valueToHash);
        byte[] hashBytes = md5.ComputeHash(inputBytes);

        return System.Convert.ToHexString(hashBytes);
    }
}