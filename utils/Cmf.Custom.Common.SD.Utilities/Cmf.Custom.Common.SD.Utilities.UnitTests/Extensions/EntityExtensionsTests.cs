using System.Collections.ObjectModel;
using Cmf.Foundation.Common;
using Cmf.Foundation.BusinessObjects;
using Cmf.Custom.Common.SD.Utilities.Extensions;
using Cmf.Foundation.BaseTestsUtils;
using Cmf.Foundation.BaseTestsUtils.Extensions;
using Cmf.Foundation.BusinessObjects.Abstractions;
using Cmf.Navigo.BusinessObjects.Abstractions;
using FluentAssertions;
using Moq;

namespace Cmf.Custom.Common.SD.Utilities.UnitTests.Extensions;

public class EntityExtensionsTests : ActionBaseTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void RemoveNullAttributes_ShouldRemoveNullAndEmptyValues(bool useEntityVersion)
    {
        Dictionary<string, object> attributes = new()
        {
            ["Null"] = null!,
            ["Empty"] = string.Empty,
            ["Value"] = "preserved"
        };
        Mock<IAttributeCollection> attributesMock = CreateAttributeCollection(attributes);

        if (useEntityVersion)
        {
            Dictionary<string, object> relatedAttributes = new()
            {
                ["RelatedNull"] = null!,
                ["RelatedValue"] = "preserved"
            };
            Mock<IEntityVersion> entityMock = new();
            entityMock.Setup(entity => entity.Attributes).Returns(attributesMock.Object);
            entityMock.Setup(entity => entity.RelatedAttributes).Returns(CreateAttributeCollection(relatedAttributes).Object);

            entityMock.Object.RemoveNullAttributes();

            relatedAttributes.Keys.Should().ContainSingle().Which.Should().Be("RelatedValue");
        }
        else
        {
            Mock<IEntity> entityMock = new();
            entityMock.Setup(entity => entity.Attributes).Returns(attributesMock.Object);

            entityMock.Object.RemoveNullAttributes();

            new Mock<IEntity>().Object.RemoveNullAttributes();
        }

        attributes.Keys.Should().ContainSingle().Which.Should().Be("Value");
    }

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

    private static Mock<IAttributeCollection> CreateAttributeCollection(Dictionary<string, object> values)
    {
        Mock<IAttributeCollection> attributesMock = new();
        attributesMock.As<IEnumerable<KeyValuePair<string, object>>>()
            .Setup(attributes => attributes.GetEnumerator())
            .Returns(() => values.GetEnumerator());
        attributesMock.Setup(attributes => attributes.Remove(It.IsAny<string>()))
            .Callback<string>(key => values.Remove(key));
        return attributesMock;
    }
[Fact]
    public void GetAttribute_ReloadsEntitiesAndValidatesMandatoryAttributes()
    {
        Dictionary<string, object> values = new();
        Mock<IAttributeCollection> attributes = new();
        attributes.Setup(x => x.ContainsKey("Priority")).Returns(() => values.ContainsKey("Priority"));
        attributes.Setup(x => x["Priority"]).Returns(() => values["Priority"]);
        Mock<IEntityInstance> entity = new();
        entity.Setup(x => x.Attributes).Returns(attributes.Object);
        entity.Setup(x => x.LoadAttributes(It.IsAny<Collection<string>>()))
            .Callback<Collection<string>>(_ => values["Priority"] = 10);

        entity.Object.GetAttribute<int>( "Priority").Should().Be(0);
        entity.Object.GetAttribute<int>( "Priority", true).Should().Be(10);
        entity.Verify(x => x.LoadAttributes(It.IsAny<Collection<string>>()), Times.Once);

        Mock<IEntityTypeProperty> mandatoryProperty = new();
        mandatoryProperty.Setup(x => x.Name).Returns("Code");
        mandatoryProperty.Setup(x => x.Description).Returns("Mandatory code");
        mandatoryProperty.Setup(x => x.PropertyType).Returns(EntityTypePropertyType.Attribute);
        IEntityTypePropertyCollection propertyCollection = new EntityTypePropertyCollection();
        propertyCollection.Add(mandatoryProperty.Object);
        Mock<IEntityType> type = new();
        type.Setup(x => x.Properties).Returns(propertyCollection);
        type.Setup(x => x.Name).Returns("Order");
        Action validate = () => type.Object.ValidateMandatoryAttributes(null);
        validate.Should().Throw<MissingMandatoryPropertyCmfException>();
        type.Verify(x => x.LoadProperties(), Times.Once);

        Mock<IEntityType> reloadType = new();
        Mock<IEntity> original = new();
        Mock<IEntity> reloaded = new();
        original.Setup(x => x.Id).Returns(12);
        original.Setup(x => x.EntityType).Returns(reloadType.Object);
        reloadType.Setup(x => x.GetInstance()).Returns(reloaded.Object);
        original.Object.ReloadEntity().Should().BeSameAs(reloaded.Object);
        reloadType.Verify(x => x.GetInstance(), Times.Once);
    }


    [Theory]
    [InlineData(true, true)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public void RequestApproval_LoadsExistingChangeSetAndOptionallyRequestsApproval(bool exists, bool requestApproval)
    {
        Mock<IChangeSet> changeSet = new();
        changeSet.Setup(x => x.ObjectExists()).Returns(exists);
        changeSet.Object.RequestApproval(requestApproval);
        changeSet.Verify(x => x.Load(), exists ? Times.Once : Times.Never);
        changeSet.Verify(x => x.RequestApproval(), exists && requestApproval ? Times.Once : Times.Never);
    }

    [Fact]
    public void RelationPresenceExtensions_LoadAndDetectRelations()
    {
        Mock<IEntityInstance> entity = new();
        entity.Object.HasRelations("Child", loadRelation: true).Should().BeFalse();
        entity.Verify(x => x.LoadRelations("Child"), Times.Once);
        Mock<IEntityRelation> childRelation = new();
        Mock<IEntityRelationCollection<IEntityRelation>> childRelations = new();
        childRelations.MockCollectionWithList(new List<IEntityRelation> { childRelation.Object });
        Mock<ICmfEntityRelationCollection> relationsByName = new();
        relationsByName.MockCollectionWithDictionary(new Dictionary<string, IEntityRelationCollection<IEntityRelation>>
        {
            ["Child"] = childRelations.Object
        });
        entity.Setup(x => x.RelationCollection).Returns(relationsByName.Object);
        entity.Object.HasRelations("Child").Should().BeTrue();

        Mock<IEntityVersion> version = new();
        version.Object.HasRelatedRelations("Parent", loadRelation: true).Should().BeFalse();
        version.Verify(x => x.LoadRelations("Parent"), Times.Once);
        Mock<IEntityRelation> parentRelation = new();
        Mock<IEntityRelationCollection<IEntityRelation>> parentRelations = new();
        parentRelations.MockCollectionWithList(new List<IEntityRelation> { parentRelation.Object });
        Mock<ICmfEntityRelationCollection> relatedRelationsByName = new();
        relatedRelationsByName.MockCollectionWithDictionary(new Dictionary<string, IEntityRelationCollection<IEntityRelation>>
        {
            ["Parent"] = parentRelations.Object
        });
        version.Setup(x => x.RelatedRelationCollection).Returns(relatedRelationsByName.Object);
        version.Object.HasRelatedRelations("Parent").Should().BeTrue();
    }
}