using Cmf.Custom.Common.SD.Utilities.Helpers;
using Cmf.Foundation.BusinessObjects.Abstractions;
using Cmf.Foundation.BusinessObjects.QueryObject;
using Cmf.Foundation.BusinessOrchestration.Abstractions;
using Cmf.Foundation.Common.Abstractions;
using Cmf.Navigo.BusinessObjects.Abstractions;
using FluentAssertions;
using Moq;
using System.Data;

namespace Cmf.Custom.Common.SD.Utilities.UnitTests.Helpers;

public class DataCollectionHelperTests
{
    private readonly Mock<IEntityFactory> _entityFactoryMock = new();

    private readonly Mock<IEntityTypeProperty> _entityTypePropertyMock = new();
    private readonly Mock<IQueryObject> _queryObjectMock = new();
    private readonly Mock<IDataCollectionInstanceCollection> _dcCollectionMock = new();
    private readonly Mock<IDataCollectionInstance> _dcInstanceMock = new();
    private readonly Mock<ILocalizationOrchestration> _localizationOrchestrationMock = new();

    public DataCollectionHelperTests()
    {
        _entityTypePropertyMock.SetupAllProperties();
        _entityTypePropertyMock.Setup(p => p.Id).Returns(100);

        _queryObjectMock.SetupAllProperties();
        _dcCollectionMock.SetupAllProperties();
        _dcInstanceMock.SetupAllProperties();

        _entityFactoryMock
            .Setup(f => f.Create<IEntityTypeProperty>())
            .Returns(_entityTypePropertyMock.Object);

        _entityFactoryMock
            .Setup(f => f.Create<IQueryObject>())
            .Returns(_queryObjectMock.Object);

        _entityFactoryMock
            .Setup(f => f.CreateCollection<IDataCollectionInstanceCollection>())
            .Returns(_dcCollectionMock.Object);

        _entityFactoryMock
            .Setup(f => f.Create<IDataCollectionInstance>())
            .Returns(_dcInstanceMock.Object);
    }

    [Fact]
    public void GetDataCollectionInstancesForMaterials_ShouldReturnEmptyCollection_WhenQueryReturnsNoData()
    {
        // Arrange
        var dataSet = TestUtilities.CreateDataSet(
            [
                new("MaterialId", typeof(long)),
                new("CreatedOn", typeof(DateTime)),
                new("Name", typeof(string))
            ],
            _ => { });

        _queryObjectMock
            .Setup(q => q.Execute(false, It.IsAny<QueryParameterCollection>()))
            .Returns(dataSet);

        var helper = new DataCollectionHelper(
            _entityFactoryMock.Object,
            _localizationOrchestrationMock.Object);

        // Act
        IDataCollectionInstanceCollection result =
            helper.GetDataCollectionInstancesForMaterials([1, 2]);

        // Assert
        result.Should().NotBeNull();

        _dcCollectionMock.Verify(
            c => c.Add(It.IsAny<IDataCollectionInstance>()),
            Times.Never);

        _dcCollectionMock.Verify(
            c => c.Load(),
            Times.Never);
    }

    [Fact]
    public void GetDataCollectionInstancesForMaterials_ShouldLoadCollection_WhenQueryReturnsData()
    {
        // Arrange
        var dataSet = TestUtilities.CreateDataSet(
            [
                new("MaterialId", typeof(long)),
                new("CreatedOn", typeof(DateTime)),
                new("Name", typeof(string))
            ],
            table =>
            {
                table.Rows.Add(1L, new DateTime(2024, 1, 1), "DC1");
                table.Rows.Add(2L, new DateTime(2024, 1, 2), "DC2");
            });

        _queryObjectMock
            .Setup(q => q.Execute(false, It.IsAny<QueryParameterCollection>()))
            .Returns(dataSet);

        var helper = new DataCollectionHelper(
            _entityFactoryMock.Object,
            _localizationOrchestrationMock.Object);

        // Act
        IDataCollectionInstanceCollection result =
            helper.GetDataCollectionInstancesForMaterials([1, 2]);

        // Assert
        result.Should().NotBeNull();

        _dcCollectionMock.Verify(
            c => c.Add(It.IsAny<IDataCollectionInstance>()),
            Times.Exactly(2));
    }

    [Fact]
    public void GetDataCollectionInstancesForMaterials_ShouldUseLatestDataCollectionInstancePerMaterial()
    {
        // Arrange
        var dataSet = TestUtilities.CreateDataSet(
            [
                new("MaterialId", typeof(long)),
                new("CreatedOn", typeof(DateTime)),
                new("Name", typeof(string))
            ],
            table =>
            {
                table.Rows.Add(1L, new DateTime(2024, 1, 1), "OldDC");
                table.Rows.Add(1L, new DateTime(2024, 2, 1), "LatestDC");
            });

        _queryObjectMock
            .Setup(q => q.Execute(false, It.IsAny<QueryParameterCollection>()))
            .Returns(dataSet);

        var helper = new DataCollectionHelper(
            _entityFactoryMock.Object,
            _localizationOrchestrationMock.Object);

        // Act
        helper.GetDataCollectionInstancesForMaterials([1]);

        // Assert
        _dcCollectionMock.Verify(
            c => c.Add(It.Is<IDataCollectionInstance>(
                dc => dc.Name == "LatestDC")),
            Times.Once);
    }
}