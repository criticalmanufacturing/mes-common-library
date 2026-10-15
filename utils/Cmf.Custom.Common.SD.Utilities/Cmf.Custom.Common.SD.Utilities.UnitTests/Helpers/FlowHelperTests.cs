using Cmf.Custom.Common.SD.Utilities.Helpers;
using Cmf.Foundation.BusinessObjects.Abstractions;
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

        var helper = new FlowHelpers(_entityFactoryMock.Object);

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

        var helper = new FlowHelpers(_entityFactoryMock.Object);

        // Act
        IFlow result = helper.GetFlowByHash("ABC123", "Hash");

        // Assert
        result.Should().NotBeNull();

        _flowMock.Verify(
            f => f.Load(),
            Times.Never);
    }
}