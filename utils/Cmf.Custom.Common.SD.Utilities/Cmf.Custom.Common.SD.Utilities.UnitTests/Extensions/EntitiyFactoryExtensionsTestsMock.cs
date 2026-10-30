using Cmf.Custom.Common.SD.Utilities.Extensions;
using Cmf.Foundation.BusinessObjects.Abstractions;
using Cmf.Foundation.Common.Abstractions;
using FluentAssertions;
using Moq;

namespace Cmf.Custom.Common.SD.Utilities.UnitTests.Extensions;

public class EntityFactoryExtensionsTests
{
    private readonly Mock<IEntityFactory> _entityFactoryMock = new();

    [Fact]
    public void CreateWithName_ShouldSetName_WhenLoadIsFalse()
    {
        // Arrange
        var entityMock = new Mock<IEntityBase>();
        entityMock.SetupAllProperties();

        _entityFactoryMock
            .Setup(f => f.Create<IEntityBase>())
            .Returns(entityMock.Object);

        // Act
        IEntityBase result =
            _entityFactoryMock.Object.CreateWithName<IEntityBase>("Entity1");

        // Assert
        result.Should().NotBeNull();
        result.Name.Should().Be("Entity1");

        entityMock.Verify(e => e.Load(), Times.Never);
    }

    [Fact]
    public void CreateWithName_ShouldLoadEntity_WhenLoadIsTrue()
    {
        // Arrange
        var entityMock = new Mock<IEntityBase>();
        entityMock.SetupAllProperties();

        _entityFactoryMock
            .Setup(f => f.Create<IEntityBase>())
            .Returns(entityMock.Object);

        // Act
        IEntityBase result =
            _entityFactoryMock.Object.CreateWithName<IEntityBase>("Entity1", true);

        // Assert
        result.Name.Should().Be("Entity1");

        entityMock.Verify(e => e.Load(), Times.Once);
    }

    [Fact]
    public void CreateCollectionWithNames_ShouldCreateCollection_WhenLoadIsFalse()
    {
        // Arrange
        var collectionMock = new Mock<IEntityBaseCollection<IEntityBase>>();

        var entities = new List<IEntityBase>();

        collectionMock
            .Setup(c => c.Add(It.IsAny<IEntityBase>()))
            .Callback<IEntityBase>(entities.Add);

        _entityFactoryMock
            .Setup(f => f.CreateCollection<IEntityBaseCollection<IEntityBase>>())
            .Returns(collectionMock.Object);

        _entityFactoryMock
            .Setup(f => f.Create<IEntityBase>())
            .Returns(() =>
            {
                var entity = new Mock<IEntityBase>();
                entity.SetupAllProperties();
                return entity.Object;
            });

        // Act
        IEntityBaseCollection<IEntityBase> result =
            _entityFactoryMock.Object.CreateCollectionWithNames<IEntityBaseCollection<IEntityBase>, IEntityBase>(
                ["Entity1", "Entity2"]);

        // Assert
        result.Should().NotBeNull();

        entities.Should().HaveCount(2);
        entities.Select(e => e.Name)
                .Should()
                .BeEquivalentTo(["Entity1", "Entity2"]);

        collectionMock.Verify(c => c.Load(), Times.Never);
    }

    [Fact]
    public void CreateCollectionWithNames_ShouldLoadCollection_WhenLoadIsTrue()
    {
        // Arrange
        var collectionMock = new Mock<IEntityBaseCollection<IEntityBase>>();

        collectionMock
            .Setup(c => c.Add(It.IsAny<IEntityBase>()));

        _entityFactoryMock
            .Setup(f => f.CreateCollection<IEntityBaseCollection<IEntityBase>>())
            .Returns(collectionMock.Object);

        _entityFactoryMock
            .Setup(f => f.Create<IEntityBase>())
            .Returns(() =>
            {
                var entity = new Mock<IEntityBase>();
                entity.SetupAllProperties();
                return entity.Object;
            });

        // Act
        IEntityBaseCollection<IEntityBase> result =
            _entityFactoryMock.Object.CreateCollectionWithNames<IEntityBaseCollection<IEntityBase>, IEntityBase>(
                ["Entity1", "Entity2"],
                true);

        // Assert
        result.Should().NotBeNull();

        collectionMock.Verify(c => c.Load(), Times.Once);
    }
}