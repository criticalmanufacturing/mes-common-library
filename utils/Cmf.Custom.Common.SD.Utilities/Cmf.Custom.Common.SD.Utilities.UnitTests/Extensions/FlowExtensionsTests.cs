using Cmf.Navigo.BusinessObjects;
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
[Fact]
    public void GetFirstAndLastFlowPathAndSteps_ShouldUseOuterFlowAndStepCorrelationIds()
    {
        var step = new Mock<IStep>();
        step.SetupGet(s => s.Name).Returns("Step-A");
        var item = new Mock<IFlowItem>();
        item.SetupGet(i => i.Type).Returns(FlowItemType.Step);
        item.SetupGet(i => i.Step).Returns(step.Object);
        item.SetupGet(i => i.CorrelationId).Returns(7L);
        var items = new Mock<IFlowItemCollection>();
        items.Setup(i => i.GetEnumerator()).Returns(() => new List<IFlowItem> { item.Object }.GetEnumerator());
        var flow = new Mock<IFlow>();
        flow.SetupGet(f => f.Name).Returns("Flow-A");
        flow.SetupGet(f => f.Revision).Returns("3");
        flow.SetupGet(f => f.FlowItems).Returns(items.Object);

        flow.Object.GetFirstFlowPathForFlow().Should().Be("Flow-A:3:1/Step-A:7");
        flow.Object.GetLastFlowPathForFlow().Should().Be("Flow-A:3:1/Step-A:7");
        flow.Object.GetFirstStepOfFlow().Should().BeSameAs(step.Object);
        flow.Object.GetLastStepOfFlow().Should().BeSameAs(step.Object);

        var childItem = new Mock<IFlowItem>();
        childItem.SetupGet(i => i.Type).Returns(FlowItemType.Step);
        childItem.SetupGet(i => i.Step).Returns(step.Object);
        childItem.SetupGet(i => i.CorrelationId).Returns(8L);
        var childItems = new Mock<IFlowItemCollection>();
        childItems.Setup(i => i.GetEnumerator()).Returns(() => new List<IFlowItem> { childItem.Object }.GetEnumerator());
        var childFlow = new Mock<IFlow>();
        childFlow.SetupGet(f => f.Name).Returns("Subflow");
        childFlow.SetupGet(f => f.Revision).Returns("2");
        childFlow.SetupGet(f => f.FlowItems).Returns(childItems.Object);
        var parentItem = new Mock<IFlowItem>();
        parentItem.SetupGet(i => i.Type).Returns(FlowItemType.Flow);
        parentItem.SetupGet(i => i.Flow).Returns(childFlow.Object);
        parentItem.SetupGet(i => i.CorrelationId).Returns(9L);
        var parentItems = new Mock<IFlowItemCollection>();
        parentItems.Setup(i => i.GetEnumerator()).Returns(() => new List<IFlowItem> { parentItem.Object }.GetEnumerator());
        var parentFlow = new Mock<IFlow>();
        parentFlow.SetupGet(f => f.Name).Returns("Parent");
        parentFlow.SetupGet(f => f.Revision).Returns("5");
        parentFlow.SetupGet(f => f.FlowItems).Returns(parentItems.Object);
        parentFlow.Object.GetFirstFlowPathForFlow().Should().Be("Parent:5:1/Subflow:2:9/Step-A:8");
        parentFlow.Object.GetLastFlowPathForFlow().Should().Be("Parent:5:1/Subflow:2:9/Step-A:8");
        parentFlow.Object.GetFirstStepOfFlow().Should().BeSameAs(step.Object);
        parentFlow.Object.GetLastStepOfFlow().Should().BeSameAs(step.Object);
    }

[Fact]
    public void GetFlowStepsFromFlow_ShouldAssignSequentialPositions()
    {
        var step = new Mock<IStep>();
        step.SetupGet(s => s.Name).Returns("Step-A");
        step.SetupGet(s => s.Id).Returns(17);
        var item = new Mock<IFlowItem>();
        item.SetupGet(i => i.Type).Returns(FlowItemType.Step);
        item.SetupGet(i => i.Step).Returns(step.Object);
        item.SetupGet(i => i.CorrelationId).Returns(1L);
        item.SetupGet(i => i.Id).Returns(18);
        var items = new Mock<IFlowItemCollection>();
        items.Setup(i => i.GetEnumerator()).Returns(() => new List<IFlowItem> { item.Object }.GetEnumerator());
        var flow = new Mock<IFlow>();
        flow.SetupGet(f => f.Name).Returns("Flow-A");
        flow.SetupGet(f => f.FlowItems).Returns(items.Object);

        Dictionary<IFlowItem, int> result = flow.Object.GetFlowStepsFromFlow();

        result.Should().ContainSingle().Which.Value.Should().Be(0);
        flow.Object.GetFlowStepsFromFlow( initialFlowPath: "Flow-A:1/Step-A:1").Should().ContainSingle();
        flow.Object.GetFlowStepsFromFlow( initialStep: step.Object).Should().ContainSingle();
        Action nullFlow = () => ((IFlow)null).GetFlowStepsFromFlow();
        nullFlow.Should().Throw<Cmf.Foundation.Common.ArgumentNullCmfException>();
        flow.Verify(f => f.Load(), Times.Exactly(3));
        flow.Verify(f => f.LoadFlowItems(), Times.Exactly(3));
    }


    [Fact]
    public void CalculateFlowPathAndMatchExtensions_HandleDirectAndNestedPaths()
    {
        var step = new Mock<IStep>();
        step.SetupGet(entity => entity.Id).Returns(2);
        step.SetupGet(entity => entity.Name).Returns("Step-A");
        var stepItem = new Mock<IFlowItem>();
        stepItem.SetupGet(item => item.Type).Returns(FlowItemType.Step);
        stepItem.SetupGet(item => item.Step).Returns(step.Object);
        stepItem.SetupGet(item => item.CorrelationId).Returns(3);
        var items = new Mock<IFlowItemCollection>();
        items.As<IEnumerable<IFlowItem>>().Setup(collection => collection.GetEnumerator()).Returns(() => new[] { stepItem.Object }.AsEnumerable().GetEnumerator());
        var flow = new Mock<IFlow>();
        flow.SetupGet(entity => entity.Id).Returns(1);
        flow.SetupGet(entity => entity.Name).Returns("Flow-A");
        flow.SetupGet(entity => entity.Revision).Returns("1");
        flow.SetupGet(entity => entity.FlowItems).Returns(items.Object);

        string directPath = flow.Object.CalculateFlowPath(step.Object);
        directPath.Should().Be("Flow-A:1:1/Step-A:3");
        "FlowA:1/StepA:2".IsFlowPathMatch(["FlowA"]).Should().BeTrue();
        "FlowB:1/StepA:2".IsFlowPathMatch(["FlowA"]).Should().BeFalse();
        directPath.IsFlowPathMatch([]).Should().BeFalse();

        var childItem = new Mock<IFlowItem>();
        childItem.SetupGet(item => item.Type).Returns(FlowItemType.Step);
        childItem.SetupGet(item => item.Step).Returns(step.Object);
        childItem.SetupGet(item => item.CorrelationId).Returns(5);
        var childItems = new Mock<IFlowItemCollection>();
        childItems.As<IEnumerable<IFlowItem>>().Setup(collection => collection.GetEnumerator()).Returns(() => new[] { childItem.Object }.AsEnumerable().GetEnumerator());
        var childFlow = new Mock<IFlow>();
        childFlow.SetupGet(entity => entity.Id).Returns(4);
        childFlow.SetupGet(entity => entity.Name).Returns("ChildFlow");
        childFlow.SetupGet(entity => entity.Revision).Returns("2");
        childFlow.SetupGet(entity => entity.FlowItems).Returns(childItems.Object);
        var nestedItem = new Mock<IFlowItem>();
        nestedItem.SetupGet(item => item.Type).Returns(FlowItemType.Flow);
        nestedItem.SetupGet(item => item.Flow).Returns(childFlow.Object);
        nestedItem.SetupGet(item => item.CorrelationId).Returns(5);
        var nestedItems = new Mock<IFlowItemCollection>();
        nestedItems.As<IEnumerable<IFlowItem>>().Setup(collection => collection.GetEnumerator()).Returns(() => new[] { nestedItem.Object }.AsEnumerable().GetEnumerator());
        flow.SetupGet(entity => entity.FlowItems).Returns(nestedItems.Object);

        flow.Object.CalculateFlowPath(step.Object).Should().Be("Flow-A:1:1/ChildFlow:2:5/Step-A:5");
        flow.Object.CalculateFlowPath(step.Object, [childFlow.Object], 7).Should().Be("Flow-A:1:7/ChildFlow:2:5/Step-A:5");
    }
}