using Cmf.Custom.Common.SD.Utilities.Helpers;
using Cmf.Foundation.Common;
using FluentAssertions;
using System.Data;
using System.Data.SqlClient;

namespace Cmf.Custom.Common.SD.Utilities.UnitTests.Helpers;

public class SqlHelperTests
{
    [Theory]
    [InlineData(null, true)]
    [InlineData("", true)]
    [InlineData("  ", true)]
    [InlineData("value", false)]
    public void InsertParameterMapsScalarAndEmptyValues(string? value, bool expectedNull)
    {
        SqlParameter parameter = SqlHelper.InsertParameter("@value", value, ParameterDirection.Input, SqlDbType.NVarChar, 20);

        parameter.ParameterName.Should().Be("@value");
        parameter.Direction.Should().Be(ParameterDirection.Input);
        parameter.SqlDbType.Should().Be(SqlDbType.NVarChar);
        parameter.Size.Should().Be(20);
        parameter.Value.Should().Be(expectedNull ? DBNull.Value : value);

        SqlParameter defaultParameter = SqlHelper.InsertParameter("@default", value, ParameterDirection.Input);
        defaultParameter.ParameterName.Should().Be("@default");
        defaultParameter.Direction.Should().Be(ParameterDirection.Input);
        defaultParameter.Value.Should().Be(expectedNull ? DBNull.Value : value);
    }

    [Theory]
    [InlineData(SQLUserDefinedTableType.Unknown, "CustomType")]
    [InlineData(SQLUserDefinedTableType.ObjectList, "CustomType")]
    public void InsertParameterMapsStructuredTableType(SQLUserDefinedTableType type, string tableName)
    {
        var value = new DataTable(tableName);

        SqlParameter parameter = SqlHelper.InsertParameter("@rows", value, ParameterDirection.Input, type);

        parameter.SqlDbType.Should().Be(SqlDbType.Structured);
        parameter.TypeName.Should().Be(type == SQLUserDefinedTableType.Unknown ? tableName : type.TypeName());
        parameter.Value.Should().BeSameAs(value);
        typeof(SqlHelper).GetMethods().Where(method => method.Name == nameof(SqlHelper.InsertParameter)).Should().HaveCount(3);
    }
}
