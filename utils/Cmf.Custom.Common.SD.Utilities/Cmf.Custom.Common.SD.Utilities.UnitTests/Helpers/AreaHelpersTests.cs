using Cmf.Custom.Common.SD.Utilities.Helpers;
using Cmf.Foundation.BusinessObjects.Abstractions;
using Cmf.Foundation.BusinessObjects.QueryObject;
using Cmf.Foundation.Common.Abstractions;
using Cmf.Navigo.BusinessObjects.Abstractions;
using FluentAssertions;
using Moq;

namespace Cmf.Custom.Common.SD.Utilities.UnitTests.Helpers;

public class AreaHelpersTests
{
    private readonly Mock<IEntityFactory> _entityFactoryMock = new();
    private readonly Mock<IQueryObject> _queryObjectMock = new();
    private readonly Mock<IAreaCollection> _areaCollectionMock = new();

    public AreaHelpersTests()
    {
        _queryObjectMock.SetupAllProperties();

        _entityFactoryMock
            .Setup(ef => ef.Create<IQueryObject>())
            .Returns(_queryObjectMock.Object);

        _entityFactoryMock
            .Setup(ef => ef.CreateCollection<IAreaCollection>())
            .Returns(_areaCollectionMock.Object);
    }


    [Fact]
    public void GetAreas_ShouldReturnAreas_WhenQueryReturnsData()
    {
        // Arrange
        var dataSet = TestUtilities.CreateDataSet(
            [new("Name", typeof(string))],
            table =>
            {
                table.Rows.Add("AREA_01");
                table.Rows.Add("AREA_02");
            });


        _queryObjectMock
            .Setup(q => q.Execute(false, It.IsAny<QueryParameterCollection>()))
            .Returns(dataSet);


        var areas = new List<IArea>();

        _areaCollectionMock
            .Setup(c => c.Add(It.IsAny<IArea>()))
            .Callback<IArea>(areas.Add);


        _entityFactoryMock
            .Setup(ef => ef.Create<IArea>())
            .Returns(() =>
            {
                var area = new Mock<IArea>();
                area.SetupAllProperties();
                return area.Object;
            });


        var helper = new AreaHelpers(
            _entityFactoryMock.Object);


        // Act
        IAreaCollection result = helper.GetAreas();


        // Assert
        result.Should().NotBeNull();

        areas.Should()
            .HaveCount(2);

        areas.Select(a => a.Name)
            .Should()
            .BeEquivalentTo(
            [
                "AREA_01",
                "AREA_02"
            ]);
    }


    [Fact]
    public void GetAreas_ShouldReturnEmptyCollection_WhenQueryReturnsNoData()
    {
        // Arrange
        var emptyDataSet = TestUtilities.CreateDataSet(
            [new("Name", typeof(string))],
            _ => { });


        _queryObjectMock
            .Setup(q => q.Execute(false, It.IsAny<QueryParameterCollection>()))
            .Returns(emptyDataSet);


        var helper = new AreaHelpers(
            _entityFactoryMock.Object);


        // Act
        IAreaCollection result = helper.GetAreas();


        // Assert
        result.Should().NotBeNull();

        _areaCollectionMock.Verify(
            c => c.Add(It.IsAny<IArea>()),
            Times.Never);
    }
}