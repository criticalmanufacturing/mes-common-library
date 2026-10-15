using Cmf.Foundation.BusinessObjects.Abstractions;
using Cmf.Foundation.Common;

namespace Cmf.Custom.Common.SD.Utilities.Exceptions
{
    /// <summary>
    /// SDUtilitiesException is a custom exception class that inherits from CmfBaseException. 
    /// It is used to represent exceptions specific to the SD Utilities module.
    /// </summary>
    /// <param name="errorMessage">Error Message</param>
    /// <param name="parameters">Parameters to format the error message.</param>
    public class SDUtilitiesException(string errorMessage, params object[] parameters) : CmfBaseException(string.Format(errorMessage, parameters))
    {
    }
}
