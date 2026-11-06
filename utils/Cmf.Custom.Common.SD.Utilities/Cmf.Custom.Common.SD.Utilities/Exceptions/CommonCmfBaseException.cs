using Cmf.Foundation.Common;
using System.Globalization;
using System.Resources;

namespace Cmf.Custom.Common.SD.Utilities.Exceptions;

public class CommonCmfBaseException : CmfBaseException
{
    private static readonly ResourceManager ResourceManager = new(
        "Cmf.Custom.Common.SD.Utilities.Exceptions.CommonExceptions",
        typeof(CommonCmfBaseException).Assembly);

    public CommonCmfBaseException(CommonExceptionType exceptionType, params string[] messageParameters)
        : base(exceptionType.ToString(), Convert.ToInt64(exceptionType),
            GetMessage(exceptionType.ToString(), messageParameters),
            GetMessage(exceptionType.ToString(), messageParameters, CultureInfo.GetCultureInfo("en-US")))
    {
    }

    private static string GetMessage(string name, string[] parameters, CultureInfo culture = null)
    {
        string message = ResourceManager.GetString(name, culture)
            ?? ResourceManager.GetString(name, CultureInfo.GetCultureInfo("en"));
        return message == null ? null : string.Format(message, parameters);
    }
}
