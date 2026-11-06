using Cmf.Foundation.Common;
using System.Data;
using System.Data.SqlClient;

namespace Cmf.Custom.Common.SD.Utilities.Helpers;

public static class SqlHelper
{
    public static SqlParameter InsertParameter(string parameterName, object value, ParameterDirection direction, SqlDbType? sqlType, int? size)
    {
        if (value == null || value is string && string.IsNullOrWhiteSpace(value.ToString()))
        {
            value = DBNull.Value;
        }

        SqlParameter parameter;
        if (sqlType.HasValue && size.HasValue)
        {
            parameter = new SqlParameter(parameterName, sqlType.Value, size.Value) { Value = value };
        }
        else
        {
            parameter = new SqlParameter(parameterName, value);
        }
        parameter.Direction = direction;
        return parameter;
    }

    public static SqlParameter InsertParameter(string parameterName, DataTable value, ParameterDirection direction, SQLUserDefinedTableType type)
    {
        SqlParameter parameter = InsertParameter(parameterName, value, direction, SqlDbType.Structured, null);
        if (type == SQLUserDefinedTableType.Unknown && !string.IsNullOrWhiteSpace(value.TableName))
        {
            parameter.TypeName = value.TableName;
        }
        else
        {
            parameter.TypeName = type.TypeName();
        }
        return parameter;
    }

    public static SqlParameter InsertParameter(string parameterName, object value, ParameterDirection direction)
    {
        return InsertParameter(parameterName, value, direction, null, null);
    }
}
