using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Cmf.Custom.Common.SD.Utilities.UnitTests;

public class TestUtilities
{
    public static DataSet CreateDataSet(DataColumn[] columns, Action<DataTable> action)
    {
        DataSet dataSet = new();
        DataTable dataTable = new();
        dataTable.Columns.AddRange(columns);
        action.Invoke(dataTable);
        dataSet.Tables.Add(dataTable);

        return dataSet;
    }
}

