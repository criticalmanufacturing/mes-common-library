using Cmf.Custom.Common.SD.Utilities.Helpers;
using Cmf.Custom.Common.SD.Utilities.Extensions;
using Cmf.Foundation.BusinessObjects.Abstractions;
using Cmf.Foundation.BaseTestsUtils.Extensions;
using Cmf.Custom.Common.SD.Utilities.Helpers.Abstractions;
using Cmf.Navigo.BusinessObjects;
using Cmf.Foundation.BusinessObjects.QueryObject;
using Cmf.Foundation.Common.Abstractions;
using Cmf.Navigo.BusinessObjects.Abstractions;
using FluentAssertions;
using Moq;
using System.Data;

namespace Cmf.Custom.Common.SD.Utilities.UnitTests.Helpers;

public class FlowHelpersTests
{
    private readonly Mock<IEntityFactory> _entityFactoryMock = new();

    private readonly Mock<IQueryObject> _queryObjectMock = new();
    private readonly Mock<IFlow> _flowMock = new();

    public FlowHelpersTests()
    {
        _queryObjectMock.SetupAllProperties();
        _flowMock.SetupAllProperties();

        _entityFactoryMock
            .Setup(f => f.Create<IQueryObject>())
            .Returns(_queryObjectMock.Object);

        _entityFactoryMock
            .Setup(f => f.Create<IFlow>())
            .Returns(_flowMock.Object);
    }

    [Fact]
    public void GetFlowByHash()
    {
        // Arrange
        var dataSet = TestUtilities.CreateDataSet(
            [
                new("Id", typeof(long)),
                new("DefinitionId", typeof(long)),
                new("Revision", typeof(int)),
                new("Name", typeof(string))
            ],
            table =>
            {
                table.Rows.Add(1, 10, 1, "FLOW_001");
            });

        _queryObjectMock
            .Setup(q => q.Execute(false, It.IsAny<QueryParameterCollection>()))
            .Returns(dataSet);

        var helper = new FlowHelpers(_entityFactoryMock.Object, Mock.Of<IGenericHelper>());

        // Act
        IFlow result = helper.GetFlowByHash("ABC123", "Hash");

        // Assert
        result.Should().NotBeNull();

        result.Name.Should().Be("FLOW_001");

        _flowMock.Verify(
            f => f.Load(),
            Times.Once);

        _queryObjectMock.Verify(
            q => q.Execute(false, It.IsAny<QueryParameterCollection>()),
            Times.Once);

        _queryObjectMock.Object.Name
            .Should()
            .Be("GetFlowByHash");

        _queryObjectMock.Object.EntityTypeName
            .Should()
            .Be("Flow");
    }

    [Fact]
    public void GetFlowByHash_WhenNoFlowExists()
    {
        // Arrange
        var dataSet = TestUtilities.CreateDataSet(
            [
                new("Id", typeof(long)),
                new("DefinitionId", typeof(long)),
                new("Revision", typeof(int)),
                new("Name", typeof(string))
            ],
            _ => { });

        _queryObjectMock
            .Setup(q => q.Execute(false, It.IsAny<QueryParameterCollection>()))
            .Returns(dataSet);

        var helper = new FlowHelpers(_entityFactoryMock.Object, Mock.Of<IGenericHelper>());

        // Act
        IFlow result = helper.GetFlowByHash("ABC123", "Hash");

        // Assert
        result.Should().NotBeNull();

        _flowMock.Verify(
            f => f.Load(),
            Times.Never);
    }


    [Fact]
    public void GetAllStepsAndFlowsWithStep_ShouldTraverseLoadedFlowItems()
    {
        var step = new Mock<IStep>();
        step.SetupGet(s => s.Name).Returns("Step-A");
        var item = new Mock<IFlowItem>();
        item.SetupGet(i => i.Type).Returns(FlowItemType.Step);
        item.SetupGet(i => i.Step).Returns(step.Object);
        var childStep = new Mock<IStep>();
        childStep.SetupGet(s => s.Name).Returns("Step-B");
        var childStepItem = new Mock<IFlowItem>();
        childStepItem.SetupGet(i => i.Type).Returns(FlowItemType.Step);
        childStepItem.SetupGet(i => i.Step).Returns(childStep.Object);
        var childItems = new Mock<IFlowItemCollection>();
        childItems.Setup(i => i.GetEnumerator()).Returns(() => new List<IFlowItem> { childStepItem.Object }.GetEnumerator());
        var childFlow = new Mock<IFlow>();
        childFlow.SetupGet(f => f.FlowItems).Returns(childItems.Object);
        var nestedItem = new Mock<IFlowItem>();
        nestedItem.SetupGet(i => i.Type).Returns(FlowItemType.Flow);
        nestedItem.SetupGet(i => i.Flow).Returns(childFlow.Object);
        var items = new Mock<IFlowItemCollection>();
        items.Setup(i => i.GetEnumerator()).Returns(() => new List<IFlowItem> { item.Object, nestedItem.Object }.GetEnumerator());
        var flow = new Mock<IFlow>();
        flow.SetupGet(f => f.FlowItems).Returns(items.Object);
        var resultSteps = new Mock<IStepCollection>();
        var resultFlows = new Mock<IFlowCollection>();
        _entityFactoryMock.Setup(f => f.CreateCollection<IStepCollection>()).Returns(resultSteps.Object);
        _entityFactoryMock.Setup(f => f.CreateCollection<IFlowCollection>()).Returns(resultFlows.Object);
        var helper = new FlowHelpers(_entityFactoryMock.Object, Mock.Of<IGenericHelper>());

        helper.GetAllStepsOfFlow(flow.Object).Should().BeSameAs(resultSteps.Object);
        helper.GetAllFlowsWithStep(flow.Object, "step-b").Should().BeSameAs(resultFlows.Object);
        resultSteps.Verify(c => c.Add(step.Object), Times.Once);
        resultSteps.Verify(c => c.Add(childStep.Object), Times.Once);
        resultFlows.Verify(c => c.Add(childFlow.Object), Times.Once);
        flow.Verify(f => f.LoadFullFlowStructure(true), Times.Exactly(2));
    }


}
