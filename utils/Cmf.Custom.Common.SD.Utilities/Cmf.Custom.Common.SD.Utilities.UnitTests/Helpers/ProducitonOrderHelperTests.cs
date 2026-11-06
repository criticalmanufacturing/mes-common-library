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

public class ProducitonOrderHelperTests
{
    private readonly Mock<IEntityFactory> _entityFactoryMock = new();
    private readonly Mock<IGenericServiceOrchestration> _genericServiceOrchestrationMock = new();
    private readonly Mock<IQueryObject> _queryObjectMock = new();
    private readonly Mock<IProductionOrderCharacteristicCollection> _productionOrderCharacteristicCollectionMock = new();

    public ProducitonOrderHelperTests()
    {
        _queryObjectMock.SetupAllProperties();
        _queryObjectMock.Setup(q => q.Execute(It.IsAny<bool>(), It.IsAny<QueryParameterCollection>())).Returns(TestUtilities.CreateDataSet([new("Id", typeof(int))], dataTable =>
        {
            dataTable.Rows.Add(1);
        }));

        _entityFactoryMock.Setup(ef => ef.Create<IQueryObject>()).Returns(_queryObjectMock.Object);
        _entityFactoryMock.Setup(ef => ef.Create<IProductionOrderCharacteristic>()).Returns(new Mock<IProductionOrderCharacteristic>().Object);
        _entityFactoryMock.Setup(ef => ef.CreateCollection<IProductionOrderCharacteristicCollection>()).Returns(_productionOrderCharacteristicCollectionMock.Object);

        _genericServiceOrchestrationMock.Setup(gs => gs.GetObjectsByFilter(It.IsAny<GetObjectsByFilterInput>())).Returns(new GetObjectsByFilterOutput()
        {
            Instance = [new Mock<IProductionOrderCharacteristic>().Object]
        });
    }

    [Fact]
    public void GetProductionOrderCharacteristics()
    {
        var mockProductionOrder = new Mock<IProductionOrder>();
        var productionOrderHelper = new ProductionOrderHelper(_entityFactoryMock.Object, _genericServiceOrchestrationMock.Object);
        IProductionOrderCharacteristicCollection productionOrderCharacteristics = productionOrderHelper.GetProductionOrderCharacteristics(mockProductionOrder.Object);

        productionOrderCharacteristics.Should().NotBeNull();
        _productionOrderCharacteristicCollectionMock.Verify(pocc => pocc.AddRange(It.IsAny<IEnumerable<IProductionOrderCharacteristic>>()), Times.AtLeastOnce);
    }
}
