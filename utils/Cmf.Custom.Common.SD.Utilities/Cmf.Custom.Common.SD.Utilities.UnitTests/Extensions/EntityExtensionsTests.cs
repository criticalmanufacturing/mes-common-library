using Cmf.Custom.Common.SD.Utilities.Extensions;
using Cmf.Foundation.BaseTestsUtils;
using Cmf.Foundation.BaseTestsUtils.Extensions;
using Cmf.Foundation.BusinessObjects.Abstractions;
using Cmf.Navigo.BusinessObjects.Abstractions;
using Moq;

namespace Cmf.Custom.Common.SD.Utilities.UnitTests.Extensions;

public class EntityExtensionsTests : ActionBaseTests
{
    [Fact]
    public void GetRelations_ShouldReturnRelations_WhenRelationExists()
    {
        //Arrange
        Mock<IMaterialResource> relation1Mock = new();
        Mock<IMaterialResource> relation2Mock = new();
        Mock<ICmfEntityRelationCollection> entityRelationCollectionMock = new();
        Mock<IEntityRelationCollection<IEntityRelation>> mockRelationCollection = new();
        mockRelationCollection.MockCollectionWithList(new List<IEntityRelation> { relation1Mock.Object, relation2Mock.Object });
        entityRelationCollectionMock.MockCollectionWithDictionary(new Dictionary<string, IEntityRelationCollection<IEntityRelation>>
        {
            { "MaterialResource", mockRelationCollection.Object }
        });

        Mock<IMaterial> materialMock = new();
        materialMock.Setup(m => m.RelationCollection).Returns(entityRelationCollectionMock.Object);

        //Act
        IEnumerable<IMaterialResource> result = materialMock.Object.GetRelations<IMaterialResource>();

        //Assert
        Assert.Equal(2, result.Count());
        Assert.Contains(relation1Mock.Object, result);
        Assert.Contains(relation2Mock.Object, result);
    }

    [Fact]
    public void GetRelations_ShouldNotReturnRelations_WhenRelationDoesNotExist()
    {
        //Arrange
        Mock<IMaterialResource> relation1Mock = new();
        Mock<IMaterialResource> relation2Mock = new();
        Mock<ICmfEntityRelationCollection> entityRelationCollectionMock = new();
        Mock<IEntityRelationCollection<IEntityRelation>> mockRelationCollection = new();
        mockRelationCollection.MockCollectionWithList(new List<IEntityRelation> { relation1Mock.Object, relation2Mock.Object });
        entityRelationCollectionMock.MockCollectionWithDictionary(new Dictionary<string, IEntityRelationCollection<IEntityRelation>>
        {
            { "MaterialResource", mockRelationCollection.Object }
        });

        Mock<IMaterial> materialMock = new();
        materialMock.Setup(m => m.RelationCollection).Returns(entityRelationCollectionMock.Object);

        //Act
        IEnumerable<IResourceContainer> result = materialMock.Object.GetRelations<IResourceContainer>();

        //Assert
        Assert.Empty(result);
    }

    [Fact]
    public void GetRelations_ShouldLoadRelations_WhenLoadRelationIsTrue()
    {
        //Arrange
        Mock<IMaterial> materialMock = new();

        //Act
        IEnumerable<IMaterialResource> result = materialMock.Object.GetRelations<IMaterialResource>(loadRelation: true, levelsToLoad: 1);

        //Assert
        materialMock.Verify(m => m.LoadRelations("MaterialResource", 1), Times.Once);
    }

    [Fact]
    public void GetRelations_ShouldNotReturnRelations_WhenRelationCollectionIsNull()
    {
        //Arrange
        Mock<IMaterial> materialMock = new();
        #pragma warning disable CS8625, CS8604
        materialMock.Setup(m => m.RelationCollection).Returns(null as ICmfEntityRelationCollection);
        #pragma warning disable CS8625, CS8604

        //Act
        IEnumerable<IMaterialResource> result = materialMock.Object.GetRelations<IMaterialResource>();

        //Assert
        Assert.Empty(result);
    }

    [Fact]
    public void GetRelationsForCollection_ShouldReturnRelations_WhenRelationExists()
    {
        //Arrange
        Mock<IMaterialResource> relation1Mock = new();
        Mock<IMaterialResource> relation2Mock = new();
        Mock<ICmfEntityRelationCollection> entityRelationCollection1Mock = new();
        Mock<IEntityRelationCollection<IEntityRelation>> mockRelationCollection1Mock = new();
        mockRelationCollection1Mock.MockCollectionWithList(new List<IEntityRelation> { relation1Mock.Object, relation2Mock.Object });
        entityRelationCollection1Mock.MockCollectionWithDictionary(new Dictionary<string, IEntityRelationCollection<IEntityRelation>>
        {
            { "MaterialResource", mockRelationCollection1Mock.Object }
        });

        Mock<IMaterial> material1Mock = new();
        material1Mock.Setup(m => m.RelationCollection).Returns(entityRelationCollection1Mock.Object);

        Mock<IMaterialResource> relation3Mock = new();
        Mock<ICmfEntityRelationCollection> entityRelationCollection2Mock = new();
        Mock<IEntityRelationCollection<IEntityRelation>> mockRelationCollection2Mock = new();
        mockRelationCollection2Mock.MockCollectionWithList(new List<IEntityRelation> { relation3Mock.Object });
        entityRelationCollection2Mock.MockCollectionWithDictionary(new Dictionary<string, IEntityRelationCollection<IEntityRelation>>
        {
            { "MaterialResource", mockRelationCollection2Mock.Object }
        });

        Mock<IMaterial> material2Mock = new();
        material2Mock.Setup(m => m.RelationCollection).Returns(entityRelationCollection2Mock.Object);

        Mock<IMaterialCollection> materialCollectionMock = new();
        materialCollectionMock.MockCollectionWithList([material1Mock.Object, material2Mock.Object]);

        //Act
        IEnumerable<IMaterialResource> result = materialCollectionMock.Object.GetRelations<IMaterialResource>();

        //Assert
        Assert.Equal(3, result.Count());
        Assert.Contains(relation1Mock.Object, result);
        Assert.Contains(relation2Mock.Object, result);
        Assert.Contains(relation3Mock.Object, result);
    }

    [Fact]
    public void GetRelationsForCollection_ShouldLoadRelations_WhenLoadRelationIsTrue()
    {
        //Arrange
        Mock<IMaterialResource> relation1Mock = new();
        Mock<IMaterialResource> relation2Mock = new();
        Mock<ICmfEntityRelationCollection> entityRelationCollection1Mock = new();
        Mock<IEntityRelationCollection<IEntityRelation>> mockRelationCollection1Mock = new();
        mockRelationCollection1Mock.MockCollectionWithList(new List<IEntityRelation> { relation1Mock.Object, relation2Mock.Object });
        entityRelationCollection1Mock.MockCollectionWithDictionary(new Dictionary<string, IEntityRelationCollection<IEntityRelation>>
        {
            { "MaterialResource", mockRelationCollection1Mock.Object }
        });

        Mock<IEntityType> entityTypeMock = new();

        Mock<IMaterial> material1Mock = new();
        material1Mock.Setup(m => m.EntityType).Returns(entityTypeMock.Object);
        material1Mock.Setup(m => m.RelationCollection).Returns(entityRelationCollection1Mock.Object);

        Mock<IMaterialCollection> collectionLoadedMock = new();
        Mock<IMaterialCollection> materialCollectionMock = new();
        materialCollectionMock.MockCollectionWithList([material1Mock.Object]);
        entityTypeMock.Setup(e => e.GetCollectionInstance()).Returns(collectionLoadedMock.Object);

        //Act
        IEnumerable<IMaterialResource> result = materialCollectionMock.Object.GetRelations<IMaterialResource>(loadRelation: true, levelsToLoad: 1);

        //Assert
        collectionLoadedMock.Verify(m => m.LoadRelations("MaterialResource", 1), Times.Once);
    }

    [Fact]
    public void GetRelationsForEntityVersion_ShouldReturnRelations_WhenRelationExists()
    {
        //Arrange
        Mock<IBOMProduct> relation1Mock = new();
        Mock<IBOMProduct> relation2Mock = new();
        Mock<ICmfEntityRelationCollection> entityRelationCollectionMock = new();
        Mock<IEntityRelationCollection<IEntityRelation>> mockRelationCollection = new();
        mockRelationCollection.MockCollectionWithList(new List<IEntityRelation> { relation1Mock.Object, relation2Mock.Object });
        entityRelationCollectionMock.MockCollectionWithDictionary(new Dictionary<string, IEntityRelationCollection<IEntityRelation>>
        {
            { "BOMProduct", mockRelationCollection.Object }
        });

        Mock<IProduct> productMock = new();
        productMock.Setup(m => m.RelationCollection).Returns(entityRelationCollectionMock.Object);

        //Act
        IEnumerable<IBOMProduct> result = productMock.Object.GetRelations<IBOMProduct>();

        //Assert
        Assert.Equal(2, result.Count());
        Assert.Contains(relation1Mock.Object, result);
        Assert.Contains(relation2Mock.Object, result);
    }

    [Fact]
    public void GetRelationsForEntityVersion_ShouldNotReturnRelations_WhenRelationDoesNotExist()
    {
        //Arrange
        Mock<IBOMProduct> relation1Mock = new();
        Mock<IBOMProduct> relation2Mock = new();
        Mock<ICmfEntityRelationCollection> entityRelationCollectionMock = new();
        Mock<IEntityRelationCollection<IEntityRelation>> mockRelationCollection = new();
        mockRelationCollection.MockCollectionWithList(new List<IEntityRelation> { relation1Mock.Object, relation2Mock.Object });
        entityRelationCollectionMock.MockCollectionWithDictionary(new Dictionary<string, IEntityRelationCollection<IEntityRelation>>
        {
            { "BOMProduct", mockRelationCollection.Object }
        });

        Mock<IProduct> productMock = new();
        productMock.Setup(m => m.RelationCollection).Returns(entityRelationCollectionMock.Object);

        //Act
        IEnumerable<IResourceContainer> result = productMock.Object.GetRelations<IResourceContainer>();

        //Assert
        Assert.Empty(result);
    }

    [Fact]
    public void GetRelationsForEntityVersion_ShouldLoadRelations_WhenLoadRelationIsTrue()
    {
        //Arrange
        Mock<IProduct> productMock = new();

        //Act
        IEnumerable<IBOMProduct> result = productMock.Object.GetRelations<IBOMProduct>(loadRelation: true, levelsToLoad: 1);

        //Assert
        productMock.Verify(m => m.LoadRelations("BOMProduct", 1), Times.Once);
    }

    [Fact]
    public void GetRelationsForEntityVersion_ShouldNotReturnRelations_WhenRelationCollectionIsNull()
    {
        //Arrange
        Mock<IProduct> productMock = new();
        #pragma warning disable CS8625, CS8604
        productMock.Setup(m => m.RelationCollection).Returns(null as ICmfEntityRelationCollection);
        #pragma warning disable CS8625, CS8604

        //Act
        IEnumerable<IBOMProduct> result = productMock.Object.GetRelations<IBOMProduct>();

        //Assert
        Assert.Empty(result);
    }

    [Fact]
    public void GetRelationsForEntityVersionCollection_ShouldReturnRelations_WhenRelationExists()
    {
        //Arrange
        Mock<IBOMProduct> relation1Mock = new();
        Mock<IBOMProduct> relation2Mock = new();
        Mock<ICmfEntityRelationCollection> entityRelationCollection1Mock = new();
        Mock<IEntityRelationCollection<IEntityRelation>> mockRelationCollection1Mock = new();
        mockRelationCollection1Mock.MockCollectionWithList(new List<IEntityRelation> { relation1Mock.Object, relation2Mock.Object });
        entityRelationCollection1Mock.MockCollectionWithDictionary(new Dictionary<string, IEntityRelationCollection<IEntityRelation>>
        {
            { "BOMProduct", mockRelationCollection1Mock.Object }
        });

        Mock<IProduct> product1Mock = new();
        product1Mock.Setup(m => m.RelationCollection).Returns(entityRelationCollection1Mock.Object);

        Mock<IBOMProduct> relation3Mock = new();
        Mock<ICmfEntityRelationCollection> entityRelationCollection2Mock = new();
        Mock<IEntityRelationCollection<IEntityRelation>> mockRelationCollection2Mock = new();
        mockRelationCollection2Mock.MockCollectionWithList(new List<IEntityRelation> { relation3Mock.Object });
        entityRelationCollection2Mock.MockCollectionWithDictionary(new Dictionary<string, IEntityRelationCollection<IEntityRelation>>
        {
            { "BOMProduct", mockRelationCollection2Mock.Object }
        });

        Mock<IProduct> product2Mock = new();
        product2Mock.Setup(m => m.RelationCollection).Returns(entityRelationCollection2Mock.Object);

        Mock<IProductCollection> productCollectionMock = new();
        productCollectionMock.MockCollectionWithList([product1Mock.Object, product2Mock.Object]);

        //Act
        IEnumerable<IBOMProduct> result = productCollectionMock.Object.GetRelations<IBOMProduct>();

        //Assert
        Assert.Equal(3, result.Count());
        Assert.Contains(relation1Mock.Object, result);
        Assert.Contains(relation2Mock.Object, result);
        Assert.Contains(relation3Mock.Object, result);
    }

    [Fact]
    public void GetRelationsForEntityVersionCollection_ShouldLoadRelations_WhenLoadRelationIsTrue()
    {
        //Arrange
        Mock<IBOMProduct> relation1Mock = new();
        Mock<IBOMProduct> relation2Mock = new();
        Mock<ICmfEntityRelationCollection> entityRelationCollection1Mock = new();
        Mock<IEntityRelationCollection<IEntityRelation>> mockRelationCollection1Mock = new();
        mockRelationCollection1Mock.MockCollectionWithList(new List<IEntityRelation> { relation1Mock.Object, relation2Mock.Object });
        entityRelationCollection1Mock.MockCollectionWithDictionary(new Dictionary<string, IEntityRelationCollection<IEntityRelation>>
        {
            { "BOMProduct", mockRelationCollection1Mock.Object }
        });

        Mock<IEntityType> entityTypeMock = new();

        Mock<IProduct> product1Mock = new();
        product1Mock.Setup(m => m.EntityType).Returns(entityTypeMock.Object);
        product1Mock.Setup(m => m.RelationCollection).Returns(entityRelationCollection1Mock.Object);

        Mock<IProductCollection> collectionLoadedMock = new();
        Mock<IProductCollection> productCollectionMock = new();
        productCollectionMock.MockCollectionWithList([product1Mock.Object]);
        entityTypeMock.Setup(e => e.GetCollectionInstance()).Returns(collectionLoadedMock.Object);

        //Act
        IEnumerable<IBOMProduct> result = productCollectionMock.Object.GetRelations<IBOMProduct>(loadRelation: true, levelsToLoad: 1);

        //Assert
        collectionLoadedMock.Verify(m => m.LoadRelations("BOMProduct", 1), Times.Once);
    }

}