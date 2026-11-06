using Cmf.Custom.Common.SD.Utilities.Extensions;
using FluentAssertions;
using System.Collections.ObjectModel;
using System.Data;

namespace Cmf.Custom.Common.SD.Utilities.UnitTests.Extensions;

public class DataSetExtensionsTests
{
    [Theory]
    [InlineData("Alpha", "Override")]
    [InlineData("Beta", "Override")]
    public void CopyAndCreateRow_ShouldApplyOverrides(string value, string overrideValue)
    {
        DataSet dataSet = CreateDataSet();
        DataTable table = dataSet.Tables["Items"]!;
        table.HasData().Should().BeFalse();
        table.Rows.Add(1, value);

        DataSet copiedDataSet = dataSet.Copy(new Dictionary<string, object> { ["Name"] = overrideValue })!;
        DataTable copiedTable = table.Copy(new Dictionary<string, object> { ["Name"] = overrideValue })!;
        DataRow unqualifiedDataSetRow = dataSet.GetNewRow(
            new Dictionary<string, object> { ["Id"] = 3, ["Name"] = value })!;
        DataRow dataSetRow = dataSet.GetNewRow(
            "Items",
            new Dictionary<string, object> { ["Id"] = 2, ["Name"] = value },
            new Dictionary<string, object> { ["Name"] = overrideValue })!;
        DataRow tableRow = table.GetNewRow(
            new Dictionary<string, object> { ["Id"] = 2, ["Name"] = value },
            new Dictionary<string, object> { ["Name"] = overrideValue })!;

        copiedDataSet.Tables["Items"]!.Rows[0]["Name"].Should().Be(overrideValue);
        copiedTable.Rows[0]["Name"].Should().Be(overrideValue);
        unqualifiedDataSetRow["Name"].Should().Be(value);
        dataSetRow["Id"].Should().Be(2);
        dataSetRow["Name"].Should().Be(overrideValue);
        tableRow["Name"].Should().Be(overrideValue);
        table.GetDataByFilter("Id = 1").Should().ContainSingle();
    }

    [Theory]
    [InlineData(1, "Alpha", true)]
    [InlineData(2, "Beta", false)]
    [InlineData(1, "Beta", true)]
    [InlineData(9, "Missing", false)]
    public void Lookups_ShouldMatchOnlyRequestedKeys(int id, string name, bool expectedMatch)
    {
        DataSet dataSet = CreateDataSet();
        DataTable table = dataSet.Tables["Items"]!;
        table.Rows.Add(1, "Alpha");

        Dictionary<string, object> lookup = new() { ["Id"] = id, ["Name"] = name };
        Collection<string> keyColumns = ["Id"];

        DataRow? tableRow = table.TryGetRecord(lookup, keyColumns);
        DataRow? dataSetRow = dataSet.TryGetRecord("Items", lookup, keyColumns);
        DataRow? anyTableRow = dataSet.TryGetRecord(lookup, keyColumns);

        (tableRow is not null).Should().Be(expectedMatch);
        (dataSetRow is not null).Should().Be(expectedMatch);
        (anyTableRow is not null).Should().Be(expectedMatch);
        table.ExactRecordExists(lookup).Should().Be(expectedMatch && id == 1 && name == "Alpha");
        dataSet.ExactRecordExists("Items", lookup).Should().Be(expectedMatch && id == 1 && name == "Alpha");
        dataSet.ExactRecordExists(lookup).Should().Be(expectedMatch && id == 1 && name == "Alpha");

        DataSet emptyDataSet = CreateDataSet();
        emptyDataSet.HasData().Should().BeFalse();
        emptyDataSet.TryGetRecord(lookup, keyColumns).Should().BeNull();
    }

    [Fact]
    public void RowsAndMerges_ShouldPreserveValuesAndConvertToNgpDataSet()
    {
        DataSet dataSet = CreateDataSet();
        DataTable table = dataSet.Tables["Items"]!;
        DataRow originalRow = table.Rows.Add(1, "Alpha");

        originalRow.GetKeyValueDictionary().Should().Contain(new KeyValuePair<string, object>("Name", "Alpha"));
        originalRow.GetValue("Id", -1).Should().Be(1);
        originalRow.GetValue("Missing", -1).Should().Be(-1);

        DataRow copiedRow = table.NewRow();
        copiedRow.CopyFrom(originalRow);
        copiedRow["Name"].Should().Be("Alpha");

        DataSet sourceDataSet = CreateDataSet();
        sourceDataSet.Tables["Items"]!.Rows.Add(2, "Beta");
        dataSet.Merge(sourceDataSet, new Collection<string> { "Id" });
        table.Rows.Count.Should().Be(2);

        DataSet importedDataSet = CreateDataSet();
        importedDataSet.ImportRow("Items", originalRow);
        importedDataSet.Tables["Items"]!.Rows.Should().ContainSingle();

        table.Merge(sourceDataSet.Tables["Items"]!, new Collection<string> { "Id" });
        table.Rows.Count.Should().Be(2);
        dataSet.HasData().Should().BeTrue();
        table.HasData().Should().BeTrue();

        var ngpDataSet = dataSet.ToNgpDataSet();
        ngpDataSet.Should().NotBeNull();
        ngpDataSet!.ConvertToDataSet()!.Tables["Items"]!.Rows.Count.Should().Be(2);

        CreateDataSet().ToNgpDataSet()!.ConvertToDataSet().Should().BeNull();
        Action invalidMerge = () => table.Merge(sourceDataSet.Tables["Items"]!, new Collection<string> { "Missing" });
        invalidMerge.Should().Throw<Exception>();
    }

    private static DataSet CreateDataSet()
    {
        DataSet dataSet = new();
        DataTable table = new("Items");
        table.Columns.Add("Id", typeof(int));
        table.Columns.Add("Name", typeof(string));
        dataSet.Tables.Add(table);
        return dataSet;
    }
}
