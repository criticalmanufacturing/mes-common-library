using Cmf.Common.CustomActionUtilities.Extensions;
using Cmf.Foundation.BusinessObjects;
using Cmf.Foundation.BusinessObjects.Abstractions;
using System.Collections.ObjectModel;
using System.Data;

namespace Cmf.Custom.Common.SD.Utilities.Extensions;

public static class NgpDataSetExtensions
{
    /// <summary>
    /// Converts an INgpDataSet to a DataSet. 
    /// If the resulting DataSet has no data, it returns null.
    /// </summary>
    /// <param name="ngpDataSet"></param>
    /// <returns></returns>
    public static DataSet ConvertToDataSet(this INgpDataSet ngpDataSet)
    {
        DataSet dataset = NgpDataSet.ToDataSet(ngpDataSet);
        if (dataset.HasData())
        {
            return dataset;
        }
        return null;
    }
}