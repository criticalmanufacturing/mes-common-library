using Cmf.Custom.Common.SD.Utilities.Helpers;
using Cmf.Foundation.BusinessObjects.Abstractions;
using Cmf.Foundation.BusinessObjects.QueryObject;
using Cmf.Foundation.BusinessOrchestration.Abstractions;
using Cmf.Foundation.BusinessOrchestration.GenericServiceManagement.InputObjects;
using Cmf.Foundation.BusinessOrchestration.GenericServiceManagement.OutputObjects;
using Cmf.Foundation.Common.Abstractions;
using Cmf.Navigo.BusinessObjects.Abstractions;
using FluentAssertions;
using Moq;
using System.Data;

namespace Cmf.Custom.Common.SD.Utilities.UnitTests.Helpers;

public class MaterialHelperTests
{
    private readonly Mock<IEntityFactory> _entityFactoryMock = new();
    private readonly Mock<IGenericServiceOrchestration> _genericServiceOrchestrationMock = new();

    private readonly Mock<IQueryObject> _queryObjectMock = new();
    private readonly Mock<IMaterialCharacteristicCollection> _materialCharacteristicCollectionMock = new();

    public MaterialHelperTests()
    {
        _queryObjectMock.SetupAllProperties();

        _entityFactoryMock
            .Setup(f => f.Create<IQueryObject>())
            .Returns(_queryObjectMock.Object);

        _entityFactoryMock
            .Setup(f => f.CreateCollection<IMaterialCharacteristicCollection>())
            .Returns(_materialCharacteristicCollectionMock.Object);

        _entityFactoryMock
            .Setup(f => f.Create<IMaterialCharacteristic>())
            .Returns(new Mock<IMaterialCharacteristic>().Object);
    }

    [Fact]
    public void GetChecklistInstanceForMaterial()
    {
        // Arrange
        var expectedDataSet = TestUtilities.CreateDataSet(
            [new("Name", typeof(string))],
            table =>
            {
                table.Rows.Add("Checklist1");
            });

        _queryObjectMock
            .Setup(q => q.Execute(false, It.IsAny<QueryParameterCollection>()))
            .Returns(expectedDataSet);

        var helper = new MaterialHelper(
            _entityFactoryMock.Object,
            _genericServiceOrchestrationMock.Object);

        // Act
        DataSet result = helper.GetChecklistInstanceForMaterial(100);

        // Assert
        result.Should().NotBeNull();

        _queryObjectMock.Verify(
            q => q.Execute(false, null),
            Times.Once);

        _queryObjectMock.Object.Name
            .Should()
            .Be("GetChecklistInstanceForMaterial");

        _queryObjectMock.Object.EntityTypeName
            .Should()
            .Be("ChecklistInstance");
    }

    [Fact]
    public void GetMaterialCharacteristics_WhenQueryReturnsData()
    {
        // Arrange
        var materialMock = new Mock<IMaterial>();
        materialMock.Setup(m => m.Id).Returns(100);

        var dataSet = TestUtilities.CreateDataSet(
            [new("Id", typeof(long)), new("Name", typeof(string))],
            table =>
            {
                table.Rows.Add(1, "Characteristic1");
                table.Rows.Add(2, "Characteristic2");
            });

        _queryObjectMock
            .Setup(q => q.Execute(false, It.IsAny<QueryParameterCollection>()))
            .Returns(dataSet);

        var characteristic1 = new Mock<IMaterialCharacteristic>().Object;
        var characteristic2 = new Mock<IMaterialCharacteristic>().Object;

        _genericServiceOrchestrationMock
            .Setup(gs => gs.GetObjectsByFilter(It.IsAny<GetObjectsByFilterInput>()))
            .Returns(new GetObjectsByFilterOutput
            {
                Instance =
                [
                    characteristic1,
                    characteristic2
                ]
            });

        var helper = new MaterialHelper(
            _entityFactoryMock.Object,
            _genericServiceOrchestrationMock.Object);

        // Act
        IMaterialCharacteristicCollection result =
            helper.GetMaterialCharacteristics(materialMock.Object);

        // Assert
        result.Should().NotBeNull();

        _genericServiceOrchestrationMock.Verify(
            gs => gs.GetObjectsByFilter(
                It.Is<GetObjectsByFilterInput>(
                    input =>
                        input.Filter.Count == 2 &&
                        input.Filter[0].Value.Equals(1L) &&
                        input.Filter[1].Value.Equals(2L))),
            Times.Once);

        _materialCharacteristicCollectionMock.Verify(
            c => c.AddRange(It.IsAny<IEnumerable<IMaterialCharacteristic>>()),
            Times.Once);
    }

    [Fact]
    public void GetMaterialCharacteristics_WhenQueryReturnsNoData()
    {
        // Arrange
        var materialMock = new Mock<IMaterial>();
        materialMock.Setup(m => m.Id).Returns(100);

        var emptyDataSet = TestUtilities.CreateDataSet(
            [new("Id", typeof(long))],
            _ => { });

        _queryObjectMock
            .Setup(q => q.Execute(false, It.IsAny<QueryParameterCollection>()))
            .Returns(emptyDataSet);

        var helper = new MaterialHelper(
            _entityFactoryMock.Object,
            _genericServiceOrchestrationMock.Object);

        // Act
        IMaterialCharacteristicCollection result =
            helper.GetMaterialCharacteristics(materialMock.Object);

        // Assert
        result.Should().NotBeNull();

        _genericServiceOrchestrationMock.Verify(
            gs => gs.GetObjectsByFilter(It.IsAny<GetObjectsByFilterInput>()),
            Times.Never);

        _materialCharacteristicCollectionMock.Verify(
            c => c.AddRange(It.IsAny<IEnumerable<IMaterialCharacteristic>>()),
            Times.Never);
    }

    [Fact]
    public void GetMaterialCharacteristics_WhenGenericServiceReturnsNoInstances()
    {
        // Arrange
        var materialMock = new Mock<IMaterial>();
        materialMock.Setup(m => m.Id).Returns(100);

        var dataSet = TestUtilities.CreateDataSet(
            [new("Id", typeof(long))],
            table =>
            {
                table.Rows.Add(1);
            });

        _queryObjectMock
            .Setup(q => q.Execute(false, It.IsAny<QueryParameterCollection>()))
            .Returns(dataSet);

        _genericServiceOrchestrationMock
            .Setup(gs => gs.GetObjectsByFilter(It.IsAny<GetObjectsByFilterInput>()))
            .Returns(new GetObjectsByFilterOutput
            {
                Instance = []
            });

        var helper = new MaterialHelper(
            _entityFactoryMock.Object,
            _genericServiceOrchestrationMock.Object);

        // Act
        IMaterialCharacteristicCollection result =
            helper.GetMaterialCharacteristics(materialMock.Object);

        // Assert
        result.Should().NotBeNull();

        _materialCharacteristicCollectionMock.Verify(
            c => c.AddRange(It.IsAny<IEnumerable<IMaterialCharacteristic>>()),
            Times.Never);
    }
}