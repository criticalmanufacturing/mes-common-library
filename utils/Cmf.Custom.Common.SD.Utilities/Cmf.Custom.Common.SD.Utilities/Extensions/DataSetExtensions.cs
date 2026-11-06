using System.Collections.ObjectModel;
using System.Data;

namespace Cmf.Custom.Common.SD.Utilities.Extensions;

public static class DataSetExtensions
{
    public static DataSet? Copy(this DataSet? dataSet, Dictionary<string, object>? overrideValues)
    {
        if (dataSet is null)
        {
            return null;
        }

        DataSet copiedDataSet = dataSet.Clone();
        if (dataSet.Tables.Count > 0)
        {
            copiedDataSet.Tables.Clear();
            foreach (DataTable sourceTable in dataSet.Tables)
            {
                copiedDataSet.Tables.Add(Copy(sourceTable, overrideValues)!);
            }
        }

        return copiedDataSet;
    }

    public static bool ExactRecordExists(this DataSet dataSet, Dictionary<string, object> rowToCheck)
    {
        foreach (DataTable table in dataSet.Tables)
        {
            if (ExactRecordExists(table, rowToCheck))
            {
                return true;
            }
        }

        return false;
    }

    public static bool ExactRecordExists(this DataSet dataSet, string tableName, Dictionary<string, object> rowToCheck)
    {
        return dataSet.Tables.Contains(tableName) && ExactRecordExists(dataSet.Tables[tableName]!, rowToCheck);
    }

    public static DataRow? TryGetRecord(this DataSet dataSet, Dictionary<string, object> dataToLookFor, Collection<string> keyColumns)
    {
        foreach (DataTable table in dataSet.Tables)
        {
            DataRow? dataRow = TryGetRecord(table, dataToLookFor, keyColumns);
            if (dataRow is not null)
            {
                return dataRow;
            }
        }

        return null;
    }

    public static DataRow? TryGetRecord(
        this DataSet dataSet,
        string tableName,
        Dictionary<string, object> dataToLookFor,
        Collection<string> keyColumns)
    {
        return dataSet.Tables.Contains(tableName)
            ? TryGetRecord(dataSet.Tables[tableName]!, dataToLookFor, keyColumns)
            : null;
    }

    public static DataRow? GetNewRow(
        this DataSet dataSet,
        Dictionary<string, object>? values = null,
        Dictionary<string, object>? overrideValues = null)
    {
        foreach (DataTable table in dataSet.Tables)
        {
            DataRow? dataRow = GetNewRow(table, values, overrideValues);
            if (dataRow is not null)
            {
                return dataRow;
            }
        }

        return null;
    }

    public static DataRow? GetNewRow(
        this DataSet dataSet,
        string tableName,
        Dictionary<string, object>? values = null,
        Dictionary<string, object>? overrideValues = null)
    {
        return dataSet.Tables.Contains(tableName)
            ? GetNewRow(dataSet.Tables[tableName]!, values, overrideValues)
            : null;
    }

    public static void ImportRow(this DataSet? dataSet, string tableName, DataRow dataRow)
    {
        if (dataSet is null || !dataSet.Tables.Contains(tableName))
        {
            return;
        }

        DataTable table = dataSet.Tables[tableName]!;
        DataRow newRow = table.NewRow();
        CopyFrom(newRow, dataRow);
        table.Rows.Add(newRow);
        table.AcceptChanges();
        dataSet.AcceptChanges();
    }

    public static void Merge(this DataSet? dataSet, DataSet? dataSetToMerge, Collection<string> keyColumns)
    {
        if (dataSet is null || dataSetToMerge is null)
        {
            return;
        }

        string[] currentTableNames = dataSet.Tables.Cast<DataTable>().Select(table => table.TableName).ToArray();
        string[] sourceTableNames = dataSetToMerge.Tables.Cast<DataTable>().Select(table => table.TableName).ToArray();

        foreach (string commonTable in currentTableNames.Intersect(sourceTableNames))
        {
            Merge(dataSet.Tables[commonTable]!, dataSetToMerge.Tables[commonTable]!, keyColumns);
        }
    }

    public static bool HasData(this DataSet? dataSet)
    {
        return dataSet is not null
            && dataSet.Tables is not null
            && dataSet.Tables.Count > 0
            && dataSet.Tables[0].Rows is not null
            && dataSet.Tables[0].Rows.Count > 0;
    }

    public static DataTable? Copy(this DataTable? table, Dictionary<string, object>? overrideValues)
    {
        if (table is null)
        {
            return null;
        }

        overrideValues ??= [];
        Dictionary<string, object> effectiveOverrides = table.Columns
            .Cast<DataColumn>()
            .ToDictionary(
                column => column.ColumnName,
                column => overrideValues.TryGetValue(column.ColumnName, out object? value) ? value : DBNull.Value);

        DataTable copiedTable = table.Clone();
        foreach (DataRow sourceRow in table.Rows)
        {
            DataRow newRow = copiedTable.NewRow();
            foreach (DataColumn column in table.Columns)
            {
                string columnName = column.ColumnName;
                newRow[columnName] = effectiveOverrides[columnName] == DBNull.Value
                    ? sourceRow[columnName]
                    : effectiveOverrides[columnName];
            }

            copiedTable.Rows.Add(newRow);
        }

        copiedTable.AcceptChanges();
        return copiedTable;
    }

    public static DataRow? GetNewRow(
        this DataTable? table,
        Dictionary<string, object>? values = null,
        Dictionary<string, object>? overrideValues = null)
    {
        if (table is null)
        {
            return null;
        }

        values ??= [];
        overrideValues ??= [];
        Dictionary<string, object> rowValues = table.Columns
            .Cast<DataColumn>()
            .ToDictionary(
                column => column.ColumnName,
                column => overrideValues.TryGetValue(column.ColumnName, out object? overrideValue)
                    ? overrideValue
                    : values.TryGetValue(column.ColumnName, out object? value) ? value : DBNull.Value);

        DataRow row = table.NewRow();
        foreach (DataColumn column in table.Columns)
        {
            object value = rowValues[column.ColumnName];
            if (value != DBNull.Value)
            {
                row[column.ColumnName] = value;
            }
        }

        return row;
    }

    public static DataRow[] GetDataByFilter(this DataTable table, string filter)
    {
        return table.Select(filter);
    }

    public static bool HasData(this DataTable dataTable)
    {
        return dataTable.Rows.Count > 0;
    }

    public static bool ExactRecordExists(this DataTable dataTable, Dictionary<string, object>? rowToCheck)
    {
        if (rowToCheck is null || rowToCheck.Count == 0)
        {
            return false;
        }

        string[] columnNames = rowToCheck.Keys.ToArray();
        HashSet<string> existingNames = dataTable.Columns
            .Cast<DataColumn>()
            .Select(column => column.ColumnName)
            .ToHashSet();
        if (!columnNames.All(existingNames.Contains))
        {
            return false;
        }

        foreach (DataRow dataRow in dataTable.Rows)
        {
            bool matches = columnNames.All(columnName =>
                string.Equals(
                    Convert.ToString(rowToCheck[columnName]),
                    Convert.ToString(dataRow[columnName]),
                    StringComparison.InvariantCultureIgnoreCase));
            if (matches)
            {
                return true;
            }
        }

        return false;
    }

    public static DataRow? TryGetRecord(
        this DataTable? sourceDataTable,
        Dictionary<string, object>? dataToLookFor,
        Collection<string>? keyColumns)
    {
        if (sourceDataTable is null
            || !HasData(sourceDataTable)
            || dataToLookFor is null
            || dataToLookFor.Count == 0
            || keyColumns is null
            || keyColumns.Count == 0)
        {
            return null;
        }

        foreach (DataRow row in sourceDataTable.Rows)
        {
            bool isFound = true;
            foreach (string keyColumn in keyColumns)
            {
                string dataTableValue = Convert.ToString(row[keyColumn])!;
                dataToLookFor.TryGetValue(keyColumn, out object? receivedValue);
                string lookupValue = Convert.ToString(receivedValue)!;

                isFound = lookupValue == dataTableValue;
                if (!isFound)
                {
                    break;
                }
            }

            if (isFound)
            {
                return row;
            }
        }

        return null;
    }

    public static void Merge(this DataTable? dataTable, DataTable? dataTableToMerge, Collection<string> keyColumns)
    {
        if (dataTableToMerge is null
            || !HasData(dataTableToMerge)
            || dataTable is null
            || keyColumns is null
            || keyColumns.Count == 0)
        {
            return;
        }

        List<string> currentKeyColumns = dataTable.Columns
            .Cast<DataColumn>()
            .Where(column => keyColumns.Contains(column.ColumnName))
            .Select(column => column.ColumnName)
            .ToList();
        List<string> sourceKeyColumns = dataTableToMerge.Columns
            .Cast<DataColumn>()
            .Where(column => keyColumns.Contains(column.ColumnName))
            .Select(column => column.ColumnName)
            .ToList();

        if (currentKeyColumns.Count == 0 || currentKeyColumns.Count != sourceKeyColumns.Count)
        {
            throw new Exception("Cannot perform merge as key columns are not found in both tables");
        }

        DataTable rowsToMerge = dataTable.Clone();
        foreach (DataRow sourceRow in dataTableToMerge.Rows)
        {
            bool rowExists = dataTable.Rows.Cast<DataRow>().Any(dataRow =>
                currentKeyColumns.All(columnName => Equals(dataRow[columnName], sourceRow[columnName])));
            if (!rowExists)
            {
                DataRow newRow = rowsToMerge.NewRow();
                CopyFrom(newRow, sourceRow);
                rowsToMerge.Rows.Add(newRow);
            }
        }

        if (HasData(rowsToMerge))
        {
            dataTable.Merge(rowsToMerge);
        }
    }

    public static void CopyFrom(this DataRow dataRow, DataRow sourceDataRow)
    {
        for (int position = 0; position < dataRow.Table.Columns.Count; position++)
        {
            dataRow[position] = sourceDataRow[position];
        }
    }

    public static Dictionary<string, object>? GetKeyValueDictionary(this DataRow? dataRow)
    {
        return dataRow?.Table.Columns
            .Cast<DataColumn>()
            .ToDictionary(column => column.ColumnName, column => dataRow[column.ColumnName]);
    }

    public static T GetValue<T>(this DataRow dataRow, string fieldName, T defaultValue = default!)
    {
        if (dataRow.Table.Columns.Contains(fieldName)
            && dataRow[fieldName] != DBNull.Value
            && dataRow[fieldName] is T value)
        {
            return value;
        }

        return defaultValue;
    }
}
