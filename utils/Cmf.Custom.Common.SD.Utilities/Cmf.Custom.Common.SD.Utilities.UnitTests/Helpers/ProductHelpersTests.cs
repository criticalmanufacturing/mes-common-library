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

public class ProductHelperTests
{
    private readonly Mock<IEntityFactory> _entityFactoryMock = new();
    private readonly Mock<IGenericServiceOrchestration> _genericServiceOrchestrationMock = new();

    private readonly Mock<IQueryObject> _queryObjectMock = new();
    private readonly Mock<IProductCharacteristicCollection> _productCharacteristicCollectionMock = new();

    public ProductHelperTests()
    {
        _queryObjectMock.SetupAllProperties();

        _entityFactoryMock
            .Setup(ef => ef.Create<IQueryObject>())
            .Returns(_queryObjectMock.Object);

        _entityFactoryMock
            .Setup(ef => ef.CreateCollection<IProductCharacteristicCollection>())
            .Returns(_productCharacteristicCollectionMock.Object);

        _entityFactoryMock
            .Setup(ef => ef.Create<IProductCharacteristic>())
            .Returns(new Mock<IProductCharacteristic>().Object);
    }


    [Fact]
    public void GetProductCharacteristics_ShouldReturnCharacteristics_WhenQueryReturnsData()
    {
        // Arrange
        var productMock = new Mock<IProduct>();
        productMock.Setup(p => p.Id).Returns(10);

        var dataSet = TestUtilities.CreateDataSet(
            [new("Id", typeof(long))],
            table =>
            {
                table.Rows.Add(100);
                table.Rows.Add(200);
            });

        _queryObjectMock
            .Setup(q => q.Execute(false, It.IsAny<QueryParameterCollection>()))
            .Returns(dataSet);        

        var characteristic1 = new Mock<IProductCharacteristic>().Object;
        var characteristic2 = new Mock<IProductCharacteristic>().Object;

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


        var productHelper = new ProductHelper(
            _entityFactoryMock.Object,
            _genericServiceOrchestrationMock.Object);


        // Act
        IProductCharacteristicCollection result =
            productHelper.GetProductCharacteristics(productMock.Object);


        // Assert
        result.Should().NotBeNull();

        _genericServiceOrchestrationMock.Verify(
            gs => gs.GetObjectsByFilter(
                It.Is<GetObjectsByFilterInput>(
                    input =>
                        input.Filter.Count == 2)),
            Times.Once);


        _productCharacteristicCollectionMock.Verify(
            pc =>
                pc.AddRange(It.IsAny<IEnumerable<IProductCharacteristic>>()),
            Times.Once);
    }


    [Fact]
    public void GetProductCharacteristics_ShouldReturnEmptyCollection_WhenQueryReturnsNoData()
    {
        // Arrange
        var productMock = new Mock<IProduct>();
        productMock.Setup(p => p.Id).Returns(10);


        var emptyDataSet = TestUtilities.CreateDataSet(
            [new("Id", typeof(long))],
            _ => { });


        _queryObjectMock
            .Setup(q => q.Execute(false, It.IsAny<QueryParameterCollection>()))
            .Returns(emptyDataSet);


        var productHelper = new ProductHelper(
            _entityFactoryMock.Object,
            _genericServiceOrchestrationMock.Object);


        // Act
        IProductCharacteristicCollection result =
            productHelper.GetProductCharacteristics(productMock.Object);


        // Assert
        result.Should().NotBeNull();


        _genericServiceOrchestrationMock.Verify(
            gs => gs.GetObjectsByFilter(It.IsAny<GetObjectsByFilterInput>()),
            Times.Never);


        _productCharacteristicCollectionMock.Verify(
            pc => pc.AddRange(It.IsAny<IEnumerable<IProductCharacteristic>>()),
            Times.Never);
    }


    [Fact]
    public void GetBomProducts_ShouldReturnDataSet_WhenQueryReturnsData()
    {
        // Arrange
        var materialPropertyMock = new Mock<IEntityTypeProperty>();

        materialPropertyMock.SetupAllProperties();
        materialPropertyMock.Setup(m => m.Id).Returns(50);

        _entityFactoryMock
            .Setup(ef => ef.Create<IEntityTypeProperty>())
            .Returns(materialPropertyMock.Object);


        var productMock = new Mock<IProduct>();
        productMock.Setup(p => p.Name).Returns("PRODUCT1");
        var stepMock = new Mock<IStep>();
        var materialMock = new Mock<IMaterial>();

        _entityFactoryMock
            .Setup(ef => ef.Create<IProduct>())
            .Returns(productMock.Object);
        _entityFactoryMock
            .Setup(ef => ef.Create<IStep>())
            .Returns(stepMock.Object);
        _entityFactoryMock
            .Setup(ef => ef.Create<IMaterial>())
            .Returns(materialMock.Object);


        var dataSet = TestUtilities.CreateDataSet(
        [
            new("__cmf_html_Product_Name", typeof(string)),
            new("__cmf_html_SourceStep_Name", typeof(string)),
            new("__cmf_html_AssemblyStep_Name", typeof(string))
        ],
        table =>
        {
            table.Rows.Add(
                "PRODUCT1",
                "SOURCE_STEP",
                "ASSEMBLY_STEP");
        });


        _queryObjectMock
            .Setup(q => q.Execute(false, It.IsAny<QueryParameterCollection>()))
            .Returns(dataSet);


        var productHelper = new ProductHelper(
            _entityFactoryMock.Object,
            _genericServiceOrchestrationMock.Object);


        // Act
        DataSet result = productHelper.GetBomProducts(
            new Dictionary<string, string>
            {
                { "PRODUCT1", "MATERIAL1" }
            },
            "BOM1",
            new FilterCollection());


        // Assert
        result.Should().NotBeNull();

        result.Tables[0].Columns
            .Contains("Product")
            .Should()
            .BeTrue();

        result.Tables[0].Columns
            .Contains("SourceStep")
            .Should()
            .BeTrue();

        result.Tables[0].Columns
            .Contains("AssemblyStep")
            .Should()
            .BeTrue();
    }


    [Fact]
    public void GetBomProducts_ShouldReturnEmptyDataSet_WhenQueryHasNoData()
    {
        // Arrange
        var materialPropertyMock = new Mock<IEntityTypeProperty>();

        materialPropertyMock.SetupAllProperties();
        materialPropertyMock.Setup(m => m.Id).Returns(50);


        _entityFactoryMock
            .Setup(ef => ef.Create<IEntityTypeProperty>())
            .Returns(materialPropertyMock.Object);


        var dataSet = TestUtilities.CreateDataSet(
            [new("Id", typeof(long))],
            _ => { });


        _queryObjectMock
            .Setup(q => q.Execute(false, It.IsAny<QueryParameterCollection>()))
            .Returns(dataSet);


        var productHelper = new ProductHelper(
            _entityFactoryMock.Object,
            _genericServiceOrchestrationMock.Object);


        // Act
        DataSet result = productHelper.GetBomProducts(
            [],
            "BOM1",
            new FilterCollection());


        // Assert
        result.Should().NotBeNull();

        result.Tables[0].Rows.Count
            .Should()
            .Be(0);
    }
}