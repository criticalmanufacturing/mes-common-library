using Cmf.Custom.Common.SD.Utilities.Helpers;
using Cmf.Custom.Common.SD.Utilities.Extensions;
using Cmf.Foundation.BaseTestsUtils.Extensions;
using Cmf.Foundation.BusinessObjects.Abstractions;
using Cmf.Custom.Common.SD.Utilities.Helpers.Abstractions;
using Cmf.Navigo.BusinessOrchestration.Abstractions;
using Cmf.Navigo.BusinessOrchestration.MaterialManagement.InputObjects;
using Cmf.Navigo.BusinessOrchestration.MaterialManagement.OutputObjects;
using Cmf.Foundation.BusinessObjects.QueryObject;
using Cmf.Foundation.BusinessOrchestration.Abstractions;
using Cmf.Foundation.BusinessOrchestration.GenericServiceManagement.InputObjects;
using Cmf.Foundation.Common;
using Cmf.Foundation.BusinessOrchestration;
using Cmf.Foundation.BusinessOrchestration.GenericServiceManagement.OutputObjects;
using Cmf.Foundation.Common.Abstractions;
using Cmf.Navigo.BusinessObjects.Abstractions;
using Cmf.Navigo.BusinessObjects;
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
            _genericServiceOrchestrationMock.Object,
            Mock.Of<IDEEHelper>(),
            Mock.Of<IMaterialOrchestration>());

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
            _genericServiceOrchestrationMock.Object,
            Mock.Of<IDEEHelper>(),
            Mock.Of<IMaterialOrchestration>());

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
            _genericServiceOrchestrationMock.Object,
            Mock.Of<IDEEHelper>(),
            Mock.Of<IMaterialOrchestration>());

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
            _genericServiceOrchestrationMock.Object,
            Mock.Of<IDEEHelper>(),
            Mock.Of<IMaterialOrchestration>());

        // Act
        IMaterialCharacteristicCollection result =
            helper.GetMaterialCharacteristics(materialMock.Object);

        // Assert
        result.Should().NotBeNull();

        _materialCharacteristicCollectionMock.Verify(
            c => c.AddRange(It.IsAny<IEnumerable<IMaterialCharacteristic>>()),
            Times.Never);
    }

    [Fact]
    public void TotalPrimaryQuantity_ShouldIncludeSubMaterialQuantities()
    {
        var material = new Mock<IMaterial>();
        material.SetupGet(m => m.PrimaryQuantity).Returns(4m);
        material.SetupGet(m => m.SubMaterialsPrimaryQuantity).Returns(7m);
        var helper = new MaterialHelper(_entityFactoryMock.Object, _genericServiceOrchestrationMock.Object, Mock.Of<IDEEHelper>(), Mock.Of<IMaterialOrchestration>());

        helper.GetMaterialsInMaterialStructure((IMaterialCollection)null, "Lot").Should().BeNull();
        helper.GetLotsInMaterialStructure((IMaterialCollection)null).Should().BeNull();
        helper.GetLotsInContainer(Mock.Of<IContainer>()).Should().BeNull();
        helper.GetAttachedConsumables(Mock.Of<IResource>()).Should().BeEmpty();

        var consumable = Mock.Of<IMaterial>();
        var feed = new Mock<IResource>();
        feed.SetupGet(r => r.ProcessingType).Returns(ProcessingType.ConsumableFeed);
        var materialInfo = new Mock<IResourceMaterialInformation>();
        materialInfo.SetupGet(i => i.Material).Returns(consumable);
        materialInfo.SetupGet(i => i.Order).Returns(3);
        var materialInfos = new Mock<IResourceMaterialInformationCollection>();
        materialInfos.MockCollectionWithList(new List<IResourceMaterialInformation> { materialInfo.Object });
        feed.Setup(r => r.GetResourceMaterialInformation(true)).Returns(materialInfos.Object);
        var subResources = new Mock<IResourceCollection>();
        subResources.MockCollectionWithList(new List<IResource> { feed.Object });
        var mainResource = new Mock<IResource>();
        mainResource.SetupGet(r => r.SubResources).Returns(subResources.Object);
        var attached = helper.GetAttachedConsumables(mainResource.Object, 2);
        attached.Should().ContainSingle();
        attached[0].Consumable.Should().BeSameAs(consumable);
        attached[0].ConsumableFeed.Should().BeSameAs(feed.Object);
        attached[0].Order.Should().Be(3);
    }

    [Fact]
    public void GetStepParentFlow_ShouldReturnMaterialFlow_WhenFlowPathHasNoNestedFlow()
    {
        var flow = Mock.Of<IFlow>();
        var material = new Mock<IMaterial>();
        material.SetupGet(m => m.FlowPath).Returns("Flow-A:1:1/Step-A:2");
        material.SetupGet(m => m.Flow).Returns(flow);
        var helper = new MaterialHelper(_entityFactoryMock.Object, _genericServiceOrchestrationMock.Object, Mock.Of<IDEEHelper>(), Mock.Of<IMaterialOrchestration>());

        helper.GetStepParentFlow(material.Object).Should().BeSameAs(flow);

        var nestedFlow = new Mock<IFlow>();
        var nestedMaterial = new Mock<IMaterial>();
        nestedMaterial.SetupGet(m => m.FlowPath).Returns("Flow-A:1:1/Subflow:2/Step-A:4");
        _entityFactoryMock.Setup(f => f.Create<IFlow>()).Returns(nestedFlow.Object);
        helper.GetStepParentFlow(nestedMaterial.Object).Should().BeSameAs(nestedFlow.Object);
        nestedFlow.Verify(f => f.Load("Subflow"), Times.Once);

        var bomFlows = new Mock<IFlowCollection>();
        bomFlows.MockCollectionWithList(new List<IFlow>());
        var bomStep = new Mock<IStep>();
        var emptyBoms = new Mock<IBOMProductCollection>();
        _entityFactoryMock.Setup(f => f.CreateCollection<IFlowCollection>()).Returns(bomFlows.Object);
        _entityFactoryMock.Setup(f => f.Create<IStep>()).Returns(bomStep.Object);
        _entityFactoryMock.Setup(f => f.CreateCollection<IBOMProductCollection>()).Returns(emptyBoms.Object);
        var bomMaterial = new Mock<IMaterial>();
        bomMaterial.SetupGet(m => m.FlowPath).Returns("Subflow:2:1/Step-A:4");
        bomMaterial.SetupGet(m => m.Product).Returns(Mock.Of<IProduct>(p => p.Name == "Product-A"));
        helper.ResolveBOMProducts(bomMaterial.Object).Should().BeSameAs(emptyBoms.Object);
    }

    [Fact]
    public void MoveMaterialToStep_ShouldAppendCommentAndMergeServiceOutput()
    {
        var orchestration = new Mock<IMaterialOrchestration>();
        var output = new TestServiceOutput { Message = "original" };
        var moveOutput = new Cmf.Navigo.BusinessOrchestration.MaterialManagement.OutputObjects.MoveMaterialToStepOutput { Message = "moved" };
        orchestration.Setup(o => o.MoveMaterialToStep(It.IsAny<Cmf.Navigo.BusinessOrchestration.MaterialManagement.InputObjects.MoveMaterialToStepInput>())).Returns(moveOutput);
        var helper = new MaterialHelper(_entityFactoryMock.Object, _genericServiceOrchestrationMock.Object, Mock.Of<IDEEHelper>(), orchestration.Object);
        var material = Mock.Of<IMaterial>();
        var input = new TestServiceInput { ServiceComments = "existing" };

        helper.MoveMaterialToStep(material, "Flow-A:1:1/Step-A:1", output, input, "moving");

        orchestration.Verify(o => o.MoveMaterialToStep(It.Is<Cmf.Navigo.BusinessOrchestration.MaterialManagement.InputObjects.MoveMaterialToStepInput>(i => i.Material == material && i.ServiceComments == $"existing{Environment.NewLine}moving")), Times.Once);
        output.Message.Should().Be($"original{Environment.NewLine}moved");

        var renameMaterial = new Mock<IMaterial>();
        renameMaterial.SetupGet(m => m.SubMaterialCount).Returns(0);
        renameMaterial.SetupGet(m => m.PrimaryQuantity).Returns(1m);
        renameMaterial.SetupGet(m => m.SecondaryQuantity).Returns(2m);
        var splitOutput = new SplitMaterialOutput { Message = "split" };
        orchestration.Setup(o => o.SplitMaterial(It.IsAny<SplitMaterialInput>())).Returns(splitOutput);
        helper.RenameMaterial(renameMaterial.Object, "Renamed", output, input, "rename", "renamed").Should().BeNull();
        orchestration.Verify(o => o.SplitMaterial(It.Is<SplitMaterialInput>(i => i.ChildMaterials[0].Name == "Renamed" && i.ServiceComments == $"existing{Environment.NewLine}rename")), Times.Once);
        output.Message.Should().Be($"original{Environment.NewLine}moved{Environment.NewLine}split");
        output.FeedbackMessages.Should().ContainSingle().Which.Message.Should().Be("renamed");
    }

    private sealed class TestServiceInput : BaseInput { }
    private sealed class TestServiceOutput : BaseOutput { }

}
