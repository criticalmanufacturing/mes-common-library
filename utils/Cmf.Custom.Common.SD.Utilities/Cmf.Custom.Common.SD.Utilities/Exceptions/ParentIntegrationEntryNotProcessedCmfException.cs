namespace Cmf.Custom.Common.SD.Utilities.Exceptions;

public class ParentIntegrationEntryNotProcessedCmfException(string parentIntegrationName)
    : CommonCmfBaseException(CommonExceptionType.cm1230001, parentIntegrationName)
{
}
