using Cmf.Custom.Common.SD.Utilities.Helpers;
using Cmf.Custom.Common.SD.Utilities.Extensions;
using Cmf.Custom.Common.SD.Utilities.Helpers.Abstractions;
using Cmf.Foundation.Common;
using Cmf.Foundation.Common.Abstractions;
using Cmf.Navigo.BusinessObjects;
using Cmf.Navigo.BusinessObjects.Abstractions;
using FluentAssertions;
using Moq;

namespace Cmf.Custom.Common.SD.Utilities.UnitTests.Helpers;

public class FlowSearchHelperTests
{


    [Theory]
    [InlineData(2, true)]
    [InlineData(-1, false)]
    public void GetFlowStepFromFlowPathCreatesAndLoadsNamedEntities(int levelsToLoad, bool shouldLoad)
    {
        var entityFactory = new Mock<IEntityFactory>();
        var flow = new Mock<IFlow>();
        var step = new Mock<IStep>();
        var flows = new Mock<IFlowCollection>();
        flows.SetupGet(collection => collection.Count).Returns(1);
        flows.As<IEnumerable<IFlow>>().Setup(collection => collection.GetEnumerator()).Returns(() => new[] { flow.Object }.AsEnumerable().GetEnumerator());
        flow.SetupAllProperties();
        step.SetupAllProperties();
        entityFactory.Setup(factory => factory.Create<IFlow>()).Returns(flow.Object);
        entityFactory.Setup(factory => factory.Create<IStep>()).Returns(step.Object);
        entityFactory.Setup(factory => factory.CreateCollection<IFlowCollection>()).Returns(flows.Object);

        var helper = new FlowSearchHelper(entityFactory.Object);
        helper.GetFlowStepFromFlowPath("FlowA:1/StepA:2", out IFlowCollection resultFlows, out IStep resultStep, levelsToLoad);

        resultFlows.Should().BeSameAs(flows.Object);
        flow.Object.Name.Should().Be("FlowA");
        step.Object.Name.Should().Be("StepA");
        flows.Verify(collection => collection.Load(levelsToLoad), shouldLoad ? Times.Once() : Times.Never());
        step.Verify(entity => entity.Load(levelsToLoad), shouldLoad ? Times.Once() : Times.Never());

        if (levelsToLoad == 2)
        {
            Action emptyPath = () => helper.GetFlowStepFromFlowPath(string.Empty, out _, out _);
            emptyPath.Should().Throw<ArgumentNullCmfException>().WithMessage("*FlowPath*");
        }
    }


    [Theory]
    [InlineData(null, "StepA", "FlowName")]
    [InlineData("FlowA", null, "StepName")]
    public void CalculateFlowPathValidatesArgumentsAndBuildsMatchingPaths(string? flowName, string? stepName, string parameterName)
    {
        var invalidFactory = new Mock<IEntityFactory>();
        invalidFactory.Setup(factory => factory.Create<IFlow>()).Returns(Mock.Of<IFlow>());
        var invalidHelper = new FlowSearchHelper(invalidFactory.Object);
        Action act = () => invalidHelper.CalculateFlowPath(flowName, stepName);
        act.Should().Throw<ArgumentNullCmfException>().WithMessage($"*{parameterName}*");

        var pathFactory = new Mock<IEntityFactory>();
        var flow = new Mock<IFlow>();
        flow.SetupGet(entity => entity.Id).Returns(1);
        flow.SetupGet(entity => entity.Name).Returns("FlowA");
        flow.SetupGet(entity => entity.Revision).Returns("1");
        var step = new Mock<IStep>();
        step.SetupGet(entity => entity.Id).Returns(2);
        step.SetupGet(entity => entity.Name).Returns("StepA");
        var stepItem = new Mock<IFlowItem>();
        stepItem.SetupGet(item => item.Type).Returns(FlowItemType.Step);
        stepItem.SetupGet(item => item.Step).Returns(step.Object);
        stepItem.SetupGet(item => item.CorrelationId).Returns(3);
        var items = new Mock<IFlowItemCollection>();
        items.As<IEnumerable<IFlowItem>>().Setup(list => list.GetEnumerator()).Returns(() => new[] { stepItem.Object }.AsEnumerable().GetEnumerator());
        flow.SetupGet(entity => entity.FlowItems).Returns(items.Object);
        pathFactory.Setup(factory => factory.Create<IFlow>()).Returns(flow.Object);
        pathFactory.Setup(factory => factory.Create<IStep>()).Returns(step.Object);
        new FlowSearchHelper(pathFactory.Object).CalculateFlowPath("FlowA", "StepA").Should().Be("FlowA:1:1/StepA:3");
        typeof(IFlowSearchHelper).GetMethod(nameof(IFlowSearchHelper.CalculateFlowPath), [typeof(string), typeof(string), typeof(string[])])!.GetParameters()[2].DefaultValue.Should().BeNull();
        typeof(IFlowSearchHelper).GetMethod(nameof(IFlowSearchHelper.GetFlowStepFromFlowPath))!.GetParameters()[3].DefaultValue.Should().Be(0);
    }

}
