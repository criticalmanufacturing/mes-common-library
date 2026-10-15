using Cmf.Custom.Common.SD.Utilities.Helpers;
using Cmf.Foundation.BusinessObjects.Abstractions;
using Cmf.Foundation.BusinessObjects.QueryObject;
using Cmf.Foundation.Common.Abstractions;
using Cmf.Navigo.BusinessObjects.Abstractions;
using FluentAssertions;
using Moq;
using System.Data;

namespace Cmf.Custom.Common.SD.Utilities.UnitTests.Helpers;

public class BOMHelperTests
{
    private readonly Mock<IEntityFactory> _entityFactoryMock = new();
    private readonly Mock<IQueryObject> _queryObjectMock = new();
    private readonly Mock<IBOMCollection> _bomCollectionMock = new();

    public BOMHelperTests()
    {
        _queryObjectMock.SetupAllProperties();

        _entityFactoryMock
            .Setup(f => f.Create<IQueryObject>())
            .Returns(_queryObjectMock.Object);

        _entityFactoryMock
            .Setup(f => f.CreateCollection<IBOMCollection>())
            .Returns(_bomCollectionMock.Object);

        _entityFactoryMock
            .Setup(f => f.Create<IBOM>())
            .Returns(() => new Mock<IBOM>().Object);
    }

    [Fact]
    public void GetBOMAssociatedWithProducts_ShouldReturnEmptyCollection_WhenQueryReturnsNoData()
    {
        // Arrange
        var dataSet = TestUtilities.CreateDataSet(
            [new("SourceEntityName", typeof(string))],
            _ => { });

        _queryObjectMock
            .Setup(q => q.Execute(false, It.IsAny<QueryParameterCollection>()))
            .Returns(dataSet);

        var helper = new BOMHelper(_entityFactoryMock.Object);

        // Act
        IBOMCollection result = helper.GetBOMAssociatedWithProducts(["P1"]);

        // Assert
        result.Should().NotBeNull();

        _bomCollectionMock.Verify(
            c => c.Add(It.IsAny<IBOM>()),
            Times.Never);
    }

    [Fact]
    public void GetBOMByHash_ShouldReturnBom_WhenQueryReturnsData()
    {
        // Arrange
        var bomMock = new Mock<IBOM>();
        bomMock.SetupAllProperties();

        _entityFactoryMock
            .Setup(f => f.Create<IBOM>())
            .Returns(bomMock.Object);

        var dataSet = TestUtilities.CreateDataSet(
            [new("Name", typeof(string))],
            table =>
            {
                table.Rows.Add("BOM_001");
            });

        _queryObjectMock
            .Setup(q => q.Execute(false, It.IsAny<QueryParameterCollection>()))
            .Returns(dataSet);

        var helper = new BOMHelper(_entityFactoryMock.Object);

        // Act
        IBOM result = helper.GetBOMByHash("HASH");

        // Assert
        result.Should().NotBeNull();
        result.Name.Should().Be("BOM_001");
    }

    [Fact]
    public void GetBOMByHash_ShouldReturnEmptyBom_WhenQueryReturnsNoData()
    {
        // Arrange
        var bomMock = new Mock<IBOM>();
        bomMock.SetupAllProperties();

        _entityFactoryMock
            .Setup(f => f.Create<IBOM>())
            .Returns(bomMock.Object);

        var dataSet = TestUtilities.CreateDataSet(
            [new("Name", typeof(string))],
            _ => { });

        _queryObjectMock
            .Setup(q => q.Execute(false, It.IsAny<QueryParameterCollection>()))
            .Returns(dataSet);

        var helper = new BOMHelper(_entityFactoryMock.Object);

        // Act
        IBOM result = helper.GetBOMByHash("HASH");

        // Assert
        result.Should().NotBeNull();
        result.Name.Should().BeNull();
    }
}