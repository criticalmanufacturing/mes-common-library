using Cmf.Custom.Common.SD.Utilities.Helpers;
using Cmf.Custom.Common.SD.Utilities.Extensions;
using Cmf.Foundation.BusinessObjects;
using Cmf.Foundation.BusinessObjects.Abstractions;
using Cmf.Foundation.BusinessOrchestration.Abstractions;
using Cmf.Foundation.BusinessOrchestration.EntityTypeManagement.InputObjects;
using Cmf.Foundation.BusinessOrchestration.EntityTypeManagement.OutputObjects;
using FluentAssertions;
using Moq;
using System.Collections.ObjectModel;

namespace Cmf.Custom.Common.SD.Utilities.UnitTests.Helpers;

public class AttributeHelperTests
{
    private readonly Mock<IEntityTypeOrchestration> _entityTypeOrchestration = new();
    private AttributeHelper CreateHelper() => new(_entityTypeOrchestration.Object);

    [Theory]
    [InlineData(false, 7)]
    [InlineData(true, 42)]
    public void GetAttributeValueOrDefault_LoadsOnlyWhenRequested(bool loadAttribute, int expected)
    {
        Dictionary<string, object> values = new();
        Mock<IAttributeCollection> attributes = CreateAttributes(values);
        Mock<IEntityInstance> entity = new();
        entity.Setup(x => x.Attributes).Returns(attributes.Object);
        entity.Setup(x => x.LoadAttributes(It.IsAny<Collection<string>>()))
            .Callback<Collection<string>>(_ => values["Count"] = 42);

        var helper = CreateHelper();
        helper.GetAttributeValueOrDefault(entity.Object, "Count", 7, loadAttribute).Should().Be(expected);
        helper.GetAttributeValueOrDefault(entity.Object, "Missing", 7).Should().Be(7);
        entity.Verify(x => x.LoadAttributes(It.IsAny<Collection<string>>()), loadAttribute ? Times.Once : Times.Never);
        if (loadAttribute)
        {
            helper.GetAttributeValue(entity.Object, "Count").Should().Be(42);
            helper.HasAttribute(entity.Object, "Missing").Should().BeFalse();
        }

    }

    [Theory]
    [InlineData(false, "fallback")]
    [InlineData(true, "R3")]
    public void RelatedAttributeAccess_UsesRelatedValuesAndDefaults(bool loadAttribute, string expected)
    {
        Dictionary<string, object> values = new();
        Mock<IEntityVersion> entity = new();
        entity.Setup(x => x.RelatedAttributes).Returns(CreateAttributes(values).Object);
        entity.Setup(x => x.LoadAttributes(It.IsAny<Collection<string>>()))
            .Callback<Collection<string>>(_ => values["Revision"] = "R3");

        var helper = CreateHelper();
        helper.GetRelatedAttributeValueOrDefault(entity.Object, "Revision", "fallback", loadAttribute).Should().Be(expected);
        helper.GetRelatedAttributeValue(entity.Object, "Revision", true).Should().Be("R3");
        helper.GetRelatedAttributeValueOrDefault<string>(entity.Object, "Missing").Should().BeNull();
        helper.HasRelatedAttribute(entity.Object, "Missing").Should().BeFalse();
    }

    [Fact]
    public void AddAndCompareAttributes_RespectsEntityMetadataAndLoadsInstancesAndVersions()
    {
        EntityTypeProperty property = new()
        {
            Name = "Count",
            PropertyType = EntityTypePropertyType.Attribute
        };
        IEntityTypePropertyCollection properties = new EntityTypePropertyCollection();
        properties.Add(property);
        Mock<IEntityType> entityType = new();
        entityType.Setup(x => x.Properties).Returns(properties);
        Mock<IEntityTypeOrchestration> orchestration = new();
        orchestration.Setup(x => x.GetEntityTypeByName(It.IsAny<GetEntityTypeByNameInput>()))
            .Returns(new GetEntityTypeByNameOutput { EntityType = entityType.Object });
        AttributeCollection attributes = new();
        Mock<IEntityInstance> entity = new();
        entity.Setup(x => x.Attributes).Returns(attributes);

        var helper = new AttributeHelper(orchestration.Object);
        AttributeCollection incoming = new();
        incoming["Count"] = 5;
        incoming["Unknown"] = 1;
        helper.AddAttributesToEntity(entity.Object, incoming);
        helper.HasSameAttributeValue(entity.Object, entity.Object, "Count", false).Should().BeTrue();
        helper.GetAttributeValueOrDefault(entity.Object, "Count", 0).Should().Be(5);

        EntityTypeProperty revisionProperty = new()
        {
            Name = "RevisionValue",
            PropertyType = EntityTypePropertyType.Attribute,
            BelongsTo = EntityTypeSource.Revision
        };
        EntityTypeProperty versionProperty = new()
        {
            Name = "VersionValue",
            PropertyType = EntityTypePropertyType.Attribute,
            BelongsTo = EntityTypeSource.Version
        };
        IEntityTypePropertyCollection versionedProperties = new EntityTypePropertyCollection();
        versionedProperties.Add(revisionProperty);
        versionedProperties.Add(versionProperty);
        Mock<IEntityType> versionedType = new();
        versionedType.Setup(x => x.Properties).Returns(versionedProperties);
        versionedType.Setup(x => x.IsVersioned).Returns(true);
        orchestration.Setup(x => x.GetEntityTypeByName(It.IsAny<GetEntityTypeByNameInput>()))
            .Returns(new GetEntityTypeByNameOutput { EntityType = versionedType.Object });
        AttributeCollection versionAttributes = new();
        AttributeCollection relatedAttributes = new();
        Mock<IEntityVersion> versionedEntity = new();
        versionedEntity.Setup(x => x.Attributes).Returns(versionAttributes);
        versionedEntity.Setup(x => x.RelatedAttributes).Returns(relatedAttributes);
        AttributeCollection versionedInput = new();
        versionedInput["RevisionValue"] = "R4";
        versionedInput["VersionValue"] = "V4";
        helper.AddAttributesToEntity(versionedEntity.Object, versionedInput);
        relatedAttributes["RevisionValue"].Should().Be("R4");
        versionAttributes["VersionValue"].Should().Be("V4");

    }

    private static Mock<IAttributeCollection> CreateAttributes(Dictionary<string, object> values)
    {
        Mock<IAttributeCollection> attributes = new();
        attributes.Setup(x => x.ContainsKey(It.IsAny<string>())).Returns<string>(values.ContainsKey);
        attributes.Setup(x => x[It.IsAny<string>()]).Returns<string>(key => values[key]);
        attributes.As<IEnumerable<KeyValuePair<string, object>>>()
            .Setup(x => x.GetEnumerator())
            .Returns(() => values.GetEnumerator());
        return attributes;
    }
}
