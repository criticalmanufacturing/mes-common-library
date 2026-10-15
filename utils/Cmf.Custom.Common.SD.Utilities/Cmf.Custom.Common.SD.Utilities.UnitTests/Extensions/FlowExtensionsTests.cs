using Cmf.Custom.Common.SD.Utilities.Extensions;
using Cmf.Foundation.BaseTestsUtils.Extensions;
using Cmf.Navigo.BusinessObjects.Abstractions;
using FluentAssertions;
using Moq;

namespace Cmf.Custom.Common.SD.Utilities.UnitTests.Extensions;

public class FlowExtensionsTests
{
    [Fact]
    public void GetFirstStep_ShouldReturnFirstStep_WhenFirstFlowItemIsStep()
    {
        // Arrange
        var stepMock = new Mock<IStep>();

        var flowItemMock = new Mock<IFlowItem>();
        flowItemMock.Setup(fi => fi.Step).Returns(stepMock.Object);

        var flowMock = new Mock<IFlow>();
        flowMock
            .Setup(f => f.FlowItems)
            .Returns(new Mock<IFlowItemCollection>().MockCollectionWithList(new List<IFlowItem> { flowItemMock.Object }).Object);

        // Act
        IStep result = flowMock.Object.GetFirstStep();

        // Assert
        result.Should().Be(stepMock.Object);
    }

    [Fact]
    public void GetFirstStep_ShouldReturnFirstStep_FromNestedFlow()
    {
        // Arrange
        var expectedStep = new Mock<IStep>();

        var nestedFlowItem = new Mock<IFlowItem>();
        nestedFlowItem.Setup(fi => fi.Step).Returns(expectedStep.Object);

        var nestedFlow = new Mock<IFlow>();
        nestedFlow
            .Setup(f => f.FlowItems)
            .Returns(new Mock<IFlowItemCollection>().MockCollectionWithList(new List<IFlowItem> { nestedFlowItem.Object }).Object);

        var rootFlowItem = new Mock<IFlowItem>();
        rootFlowItem.Setup(fi => fi.Flow).Returns(nestedFlow.Object);

        var rootFlow = new Mock<IFlow>();
        rootFlow
            .Setup(f => f.FlowItems)
            .Returns(new Mock<IFlowItemCollection>().MockCollectionWithList(new List<IFlowItem> { rootFlowItem.Object }).Object);

        // Act
        IStep result = rootFlow.Object.GetFirstStep();

        // Assert
        result.Should().Be(expectedStep.Object);
    }

    [Fact]
    public void GetLastStep_ShouldReturnLastStep_WhenLastFlowItemIsStep()
    {
        // Arrange
        var stepMock = new Mock<IStep>();

        var flowItemMock = new Mock<IFlowItem>();
        flowItemMock.Setup(fi => fi.Step).Returns(stepMock.Object);

        var flowMock = new Mock<IFlow>();
        flowMock
            .Setup(f => f.FlowItems)
            .Returns(new Mock<IFlowItemCollection>().MockCollectionWithList(new List<IFlowItem> { flowItemMock.Object }).Object);

        // Act
        IStep result = flowMock.Object.GetLastStep();

        // Assert
        result.Should().Be(stepMock.Object);
    }

    [Fact]
    public void GetLastStep_ShouldReturnLastStep_FromNestedFlow()
    {
        // Arrange
        var expectedStep = new Mock<IStep>();

        var nestedFlowItem = new Mock<IFlowItem>();
        nestedFlowItem.Setup(fi => fi.Step).Returns(expectedStep.Object);

        var nestedFlow = new Mock<IFlow>();
        nestedFlow
            .Setup(f => f.FlowItems)
            .Returns(new Mock<IFlowItemCollection>().MockCollectionWithList(new List<IFlowItem> { nestedFlowItem.Object }).Object);

        var rootFlowItem = new Mock<IFlowItem>();
        rootFlowItem.Setup(fi => fi.Flow).Returns(nestedFlow.Object);

        var rootFlow = new Mock<IFlow>();
        rootFlow
            .Setup(f => f.FlowItems)
            .Returns(new Mock<IFlowItemCollection>().MockCollectionWithList(new List<IFlowItem> { rootFlowItem.Object }).Object);

        // Act
        IStep result = rootFlow.Object.GetLastStep();

        // Assert
        result.Should().Be(expectedStep.Object);
    }
}