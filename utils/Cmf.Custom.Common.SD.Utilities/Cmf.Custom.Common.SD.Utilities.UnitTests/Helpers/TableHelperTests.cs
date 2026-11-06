using Cmf.Custom.Common.SD.Utilities.Helpers;
using Cmf.Custom.Common.SD.Utilities.Extensions;
using Cmf.Custom.Common.SD.Utilities.Helpers.Abstractions;
using Cmf.Foundation.BusinessObjects;
using Cmf.Foundation.BusinessObjects.Abstractions;
using Cmf.Foundation.Common.Abstractions;
using FluentAssertions;
using Moq;
using System.Data;

namespace Cmf.Custom.Common.SD.Utilities.UnitTests.Helpers;

public class TableHelperTests
{
    [Fact]
    public void LookupOperationsAddAndFilterValues()
    {
        var values = new Cmf.Foundation.BusinessObjects.LookupTableValueCollection
        {
            Mock.Of<ILookupTableValue>(value => value.Value == "Available" && value.Description == "Existing"),
            Mock.Of<ILookupTableValue>(value => value.Value == "Beta" && value.Description == "Second")
        };
        var table = new Mock<ILookupTable>();
        table.SetupGet(entity => entity.Id).Returns(12);
        table.SetupGet(entity => entity.Values).Returns(values);
        var helper = new TestableTableHelper(Mock.Of<IEntityFactory>(), Mock.Of<Cmf.Foundation.BusinessOrchestration.Abstractions.IChangeSetOrchestration>())
        {
            LookupTable = table.Object
        };

        table.Object.AddValue( "available").Should().BeFalse();
        table.Object.AddValue( "NewValue", "Description").Should().BeTrue();
        helper.AddValueToLookupTable("Names", "AnotherValue").Should().BeTrue();
        table.Object.GetLookupTableValues( "bETA").Should().ContainSingle().Which.Value.Should().Be("Beta");
        helper.GetLookupTableValues("Names", "alpha", "beta").Select(value => value.Value).Should().ContainSingle().Which.Should().Be("Beta");
        helper.GetLookupTableValues("Names").Should().Contain("Available", "Beta");

        var missingTable = new Mock<ILookupTable>();
        missingTable.SetupGet(entity => entity.Id).Returns(0);
        helper.LookupTable = missingTable.Object;
        helper.AddValueToLookupTable("Missing", "Value").Should().BeFalse();
        helper.GetLookupTableValues("Missing", "Value").Should().BeEmpty();

        var valueLessTable = new Mock<ILookupTable>();
        valueLessTable.Object.AddValue( "Value").Should().BeFalse();

        table.Verify(entity => entity.Load("Names"), Times.Exactly(3));
        table.Verify(entity => entity.AddLookupTableValues(It.IsAny<ILookupTableValueCollection>()), Times.Exactly(2));
    }

    [Theory]
    [InlineData(true, false, true)]
    [InlineData(false, false, true)]
    [InlineData(false, true, true)]
    [InlineData(true, false, false)]
    public void TableQueriesAndSmartTableOperationsUseLoadedData(bool isDelete, bool hasChangeControl, bool hasData)
    {
        DataSet expectedData = TestUtilities.CreateDataSet([new DataColumn("Name", typeof(string))], table => table.Rows.Add("row"));
        INgpDataSet ngpData = NgpDataSet.FromDataSet(expectedData);
        var genericTable = new Mock<IGenericTable>();
        genericTable.SetupGet(table => table.HasData).Returns(hasData);
        genericTable.SetupGet(table => table.Data).Returns(ngpData);
        var smartTable = new Mock<ISmartTable>();
        smartTable.SetupGet(table => table.HasData).Returns(hasData);
        smartTable.SetupGet(table => table.Data).Returns(ngpData);
        smartTable.SetupGet(table => table.HasChangeControl).Returns(hasChangeControl);
        smartTable.Setup(table => table.Resolve(It.IsAny<INgpDataRow>(), true)).Returns(hasData ? ngpData : null);
        var nameGenerator = new Mock<INameGenerator>();
        nameGenerator.Setup(generator => generator.GenerateName(It.IsAny<string>())).Returns("generated");
        var changeSet = new Mock<IChangeSet>();
        changeSet.SetupAllProperties();
        var entityFactory = new Mock<IEntityFactory>();
        entityFactory.Setup(factory => factory.Create<INameGenerator>()).Returns(nameGenerator.Object);
        entityFactory.Setup(factory => factory.Create<IChangeSet>()).Returns(changeSet.Object);
        var orchestration = new Mock<Cmf.Foundation.BusinessOrchestration.Abstractions.IChangeSetOrchestration>();
        var helper = new TestableTableHelper(entityFactory.Object, orchestration.Object)
        {
            GenericTable = genericTable.Object,
            SmartTable = smartTable.Object
        };

        DataSet genericResult = helper.GetDataFromGenericTable("Generic", null);
        DataSet smartResult = helper.GetDataFromSmartTable("Smart", null);
        DataSet resolvedResult = helper.ResolveSmartTable("Smart", new Dictionary<string, string> { ["Name"] = "row" }, true);
        if (hasData)
        {
            genericResult.Should().BeEquivalentTo(expectedData);
            smartResult.Should().BeEquivalentTo(expectedData);
            resolvedResult.Should().BeEquivalentTo(expectedData);
        }
        else
        {
            genericResult.Should().BeNull();
            smartResult.Should().BeNull();
            resolvedResult.Tables.Should().BeEmpty();
        }
        helper.AddOrRemoveDataOnSmartTable("Smart", ngpData, isDelete).Should().BeSameAs(smartTable.Object);

        genericTable.Verify(table => table.Load("Generic"), Times.Once);
        genericTable.Verify(table => table.LoadData(It.Is<IFilterCollection>(filters => filters == null)), Times.Once);
        smartTable.Verify(table => table.Load("Smart"), Times.Exactly(3));
        smartTable.Verify(table => table.LoadData(It.Is<IFilterCollection>(filters => filters == null)), Times.Once);
        smartTable.Verify(table => table.Resolve(It.IsAny<INgpDataRow>(), true), Times.Once);
        smartTable.Verify(table => table.RemoveRows(ngpData, null, false), isDelete ? Times.Once() : Times.Never());
        smartTable.Verify(table => table.InsertOrUpdateRows(ngpData, false, null, null, false, false), isDelete ? Times.Never() : Times.Once());
        orchestration.Verify(orchestrator => orchestrator.RequestChangeSetApproval(It.IsAny<Cmf.Foundation.BusinessOrchestration.ChangeSetManagement.InputObjects.RequestChangeSetApprovalInput>()), hasChangeControl ? Times.Once() : Times.Never());
        if (hasChangeControl)
        {
            changeSet.Verify(entity => entity.Create(), Times.Once);
            changeSet.Object.Type.Should().Be("General");
            changeSet.Object.MakeEffectiveOnApproval.Should().BeTrue();
        }
    }

    [Fact]
    public void PublicTableMethodsRetainSourceInventorySignatures()
    {
        var methods = typeof(ITableHelper).GetMethods();

        methods.Should().HaveCount(8);
        methods.Select(method => method.Name).Should().Contain(["GetDataFromGenericTable", "GetLookupTable", "AddValueToLookupTable", "ResolveSmartTable", "GetDataFromSmartTable", "AddOrRemoveDataOnSmartTable"]);
        typeof(ITableHelper).GetMethod(nameof(ITableHelper.GetDataFromGenericTable), [typeof(string), typeof(IFilterCollection)]).Should().NotBeNull();
        typeof(ITableHelper).GetMethod(nameof(ITableHelper.GetDataFromSmartTable), [typeof(string), typeof(IFilterCollection)]).Should().NotBeNull();
        typeof(ITableHelper).GetMethod(nameof(ITableHelper.AddValueToLookupTable))!.GetParameters()[2].DefaultValue.Should().Be(string.Empty);
        typeof(ITableHelper).GetMethod(nameof(ITableHelper.AddOrRemoveDataOnSmartTable))!.GetParameters()[3].DefaultValue.Should().Be("General");
        typeof(ITableHelper).GetMethod(nameof(ITableHelper.AddOrRemoveDataOnSmartTable))!.GetParameters()[4].DefaultValue.Should().Be(true);
        typeof(TableHelper).GetMethods().Where(method => method.DeclaringType == typeof(TableHelper)).Should().HaveCount(8);
    }

    private sealed class TestableTableHelper(IEntityFactory entityFactory, Cmf.Foundation.BusinessOrchestration.Abstractions.IChangeSetOrchestration orchestration) : TableHelper(entityFactory, orchestration)
    {
        public IGenericTable GenericTable { get; set; }
        public ILookupTable LookupTable { get; set; }
        public ISmartTable SmartTable { get; set; }

        protected override IGenericTable CreateGenericTable() => GenericTable;
        protected override ILookupTable CreateLookupTableInstance() => LookupTable;
        protected override ISmartTable CreateSmartTable() => SmartTable;
        protected override INgpDataRow CreateNgpDataRow() => new NgpDataRow();
    }
}
