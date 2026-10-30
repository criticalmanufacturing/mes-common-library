using Cmf.Common.CustomActionUtilities.Extensions;
using Cmf.Foundation.BusinessObjects;
using Cmf.Foundation.BusinessObjects.Abstractions;
using System.Collections.ObjectModel;
using System.Data;

namespace Cmf.Custom.Common.SD.Utilities.Extensions;

public static class SmartTableExtensions
{
    /// <summary>
    /// Resolves a smart table using the provided values and returns the result as a DataSet.
    /// </summary>
    /// <param name="smartTable"></param>
    /// <param name="values"></param>
    /// <param name="onlyFirstRow"></param>
    /// <returns></returns>
    public static DataSet ResolveSmartTable(this ISmartTable smartTable, INgpDataRow values, bool onlyFirstRow = true)
    {
        var ngpDataSet = smartTable.Resolve(values, onlyFirstRow);
        if (ngpDataSet != null)
        {
            return ngpDataSet.ConvertToDataSet();
        }

        return null;
    }
}