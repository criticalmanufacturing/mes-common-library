using Cmf.Custom.Common.SD.Utilities.Helpers;
using Cmf.Custom.Common.SD.Utilities.Helpers.Abstractions;
using Cmf.Foundation.BaseTestsUtils.Extensions;
using Cmf.Foundation.BusinessObjects.Abstractions;
using Cmf.Foundation.BusinessObjects.QueryObject;
using Cmf.Foundation.BusinessOrchestration.Abstractions;
using Cmf.Foundation.Common.Abstractions;
using Cmf.Navigo.BusinessObjects.Abstractions;
using Cmf.Navigo.BusinessOrchestration.Abstractions;
using Cmf.Navigo.BusinessOrchestration.LaborManagement.InputObjects;
using Cmf.Navigo.BusinessOrchestration.LaborManagement.OutputObjects;
using Cmf.Navigo.BusinessOrchestration.ResourceManagement.InputObjects;
using FluentAssertions;
using Moq;
using System.Data;

namespace Cmf.Custom.Common.SD.Utilities.UnitTests.Helpers;

public class ResourceHelperTests
{
    private readonly Mock<IEntityFactory> _entityFactoryMock = new();
    private readonly Mock<ILaborOrchestration> _laborOrchestrationMock = new();
    private readonly Mock<IAreaHelpers> _areaHelpersMock = new();
    private readonly Mock<IResourceCollection> _resourceCollectionMock = new();
    public ResourceHelperTests()
    {
        _resourceCollectionMock.Setup(rc => rc.Count).Returns(1);
        _entityFactoryMock
            .Setup(ef => ef.CreateCollection<IResourceCollection>())
            .Returns(_resourceCollectionMock.Object);
    }

    [Fact]
    public void GetResourcesCheckedInByEmployee()
    {
        // Arrange
        var employeeMock = new Mock<IEmployee>();
        var areaMock = new Mock<IArea>();
        var areaCollectionMock = new Mock<IAreaCollection>();
        areaCollectionMock
            .Setup(ac => ac.GetEnumerator())
            .Returns(new List<IArea> { areaMock.Object }.GetEnumerator());

        var resourceMock = new Mock<IResource>();

        _areaHelpersMock
            .Setup(ah => ah.GetAreas())
            .Returns(areaCollectionMock.Object);

        _laborOrchestrationMock
            .Setup(lo => lo.GetResourcesCheckedInByArea(It.IsAny<GetResourcesCheckedInByAreaInput>()))
            .Returns(new GetResourcesCheckedInByAreaOutput()
            {
                Resources = _resourceCollectionMock.Object,
            });

        var resourceHelper = new ResourceHelper(
            _entityFactoryMock.Object,
            _laborOrchestrationMock.Object,
            _areaHelpersMock.Object,
            new Mock<IResourceOrchestration>().Object);

        // Act
        IResourceCollection result = resourceHelper.GetResourcesCheckedInByEmployee(employeeMock.Object);

        // Assert
        result.Should().NotBeNull();
        _resourceCollectionMock.Verify(rc => rc.AddRange(It.IsAny<IResourceCollection>()), Times.Once);

    }

    [Fact]
    public void GetChecklistInstanceForResource()
    {
        // Arrange
        var expectedDataSet = TestUtilities.CreateDataSet(
            [new("Name", typeof(string))],
            dataTable =>
            {
                dataTable.Rows.Add("Checklist1");
            });

        var queryMock = new Mock<IQueryObject>();

        queryMock
            .Setup(q => q.Execute(It.IsAny<bool>(), It.IsAny<QueryParameterCollection>()))
            .Returns(expectedDataSet);

        _entityFactoryMock.SetupAllProperties();
        _entityFactoryMock.Setup(ef => ef.Create<IQueryObject>()).Returns(queryMock.Object);

        var resourceHelper = new ResourceHelper(
            _entityFactoryMock.Object,
            _laborOrchestrationMock.Object,
            _areaHelpersMock.Object,
            new Mock<IResourceOrchestration>().Object);

        // Act
        DataSet result = resourceHelper.GetChecklistInstanceForResource(10,[ "Type1" ]);

        // Assert
        result.Should().NotBeNull();
    }

    [Fact]
    public void AttachConsumablesToResource()
    {
        // Arrange
        var materialCollectionMock = new Mock<IMaterialCollection>();
        materialCollectionMock.MockCollectionWithList(new List<IMaterial> { new Mock<IMaterial>().Object });

        _entityFactoryMock.SetupAllProperties();
        _entityFactoryMock.Setup(ef => ef.Create<IMaterialResource>()).Returns(new Mock<IMaterialResource>().Object);

        var resourceOrchestrationMock = new Mock<IResourceOrchestration>();

        var resourceHelper = new ResourceHelper(
            _entityFactoryMock.Object,
            _laborOrchestrationMock.Object,
            _areaHelpersMock.Object,
            resourceOrchestrationMock.Object);

        // Act
        resourceHelper.AttachConsumablesToResource(new Mock<IResource>().Object, materialCollectionMock.Object);

        // Assert
        resourceOrchestrationMock.Verify(ro => ro.AttachConsumablesToResource(It.IsAny<AttachConsumablesToResourceInput>()), Times.Once);
    }
}
