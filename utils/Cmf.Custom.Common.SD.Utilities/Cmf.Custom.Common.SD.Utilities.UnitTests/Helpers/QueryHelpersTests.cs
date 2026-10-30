using Cmf.Custom.Common.SD.Utilities.Helpers;
using Cmf.Foundation.BusinessObjects;
using Cmf.Foundation.BusinessObjects.Abstractions;
using Cmf.Foundation.BusinessObjects.QueryObject;
using Cmf.Foundation.Common.Abstractions;
using Cmf.Navigo.BusinessObjects.Abstractions;
using FluentAssertions;
using Microsoft.VisualBasic;
using Moq;
using System.Collections.ObjectModel;

namespace Cmf.Custom.Common.SD.Utilities.UnitTests.Helpers;

public class QueryHelpersTests
{
    private readonly Mock<IEntityFactory> _entityFactoryMock = new();
    private readonly Mock<IQueryObject> _queryObjectMock = new();

    private readonly Mock<IEntityType> _entityTypeMock = new();

    public QueryHelpersTests()
    {
        _queryObjectMock.SetupAllProperties();

        _entityFactoryMock
            .Setup(f => f.Create<IQueryObject>())
            .Returns(_queryObjectMock.Object);
    }

    [Fact]
    public void QueryEntities_ShouldReturnEmptyCollection_WhenQueryReturnsNoData()
    {
        // Arrange
        var entityMock = new Mock<IMaterial>();
        entityMock.Setup(f => f.EntityType).Returns(_entityTypeMock.Object);

        var entityCollectionMock = new Mock<IMaterialCollection>();

        _entityTypeMock
            .Setup(t => t.GetCollectionInstance())
            .Returns(entityCollectionMock.Object as Collection<IMaterial>);

        _entityFactoryMock
            .Setup(f => f.Create<IMaterial>())
            .Returns(entityMock.Object);

        var dataSet = TestUtilities.CreateDataSet(
            [new("Id", typeof(long))],
            _ => { });

        _queryObjectMock
            .Setup(q => q.Execute(false, null))
            .Returns(dataSet);

        var helper = new QueryHelpers(_entityFactoryMock.Object);

        // Act
        helper.QueryEntities<IMaterial>(new FilterCollection());

        // Assert 

        entityCollectionMock.Invocations.Should().BeEmpty();
    }
   
    [Fact]
    public void QueryEntities_ShouldReturnEntities_WhenQueryReturnsData()
    {
        // Arrange
        var entityMock = new Mock<IMaterial>();
        entityMock.Setup(f => f.EntityType).Returns(_entityTypeMock.Object);

        var entityCollectionMock = new Mock<IEntityCollection<IMaterial>>();

        _entityTypeMock
            .Setup(t => t.GetCollectionInstance())
            .Returns(entityCollectionMock.Object as Collection<IMaterial>);

        _entityFactoryMock
            .Setup(f => f.Create<IMaterial>())
            .Returns(entityMock.Object);

        var dataSet = TestUtilities.CreateDataSet(
            [new("Id", typeof(long))],
            table =>
            {
                table.Rows.Add(10L);
                table.Rows.Add(20L);
            });

        _queryObjectMock
            .Setup(q => q.Execute(false, null))
            .Returns(dataSet);

        var helper = new QueryHelpers(_entityFactoryMock.Object);

        // Act
        helper.QueryEntities<IMaterial>(new FilterCollection());

        // Assert 
        _entityFactoryMock.Verify(ef => ef.Create<IMaterial>(), Times.Exactly(3));
    }   
}