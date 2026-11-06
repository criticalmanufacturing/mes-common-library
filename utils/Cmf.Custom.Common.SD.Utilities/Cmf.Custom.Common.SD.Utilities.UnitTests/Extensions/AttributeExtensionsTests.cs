using Cmf.Custom.Common.SD.Utilities.Extensions;
using Cmf.Foundation.BusinessObjects;
using Cmf.Foundation.BusinessObjects.Abstractions;
using FluentAssertions;
using Moq;
using System.Collections.ObjectModel;

namespace Cmf.Custom.Common.SD.Utilities.UnitTests.Extensions;

public class AttributeExtensionsTests
{
    [Theory]
    [InlineData(false, 0)]
    [InlineData(true, 9)]
    public void RelationAttributeValue_LoadsOnlyWhenRequested(bool loadAttribute, int expected)
    {
        Dictionary<string, object> values = new();
        var attributes = CreateAttributes(values);
        var relation = new Mock<IEntityRelation>();
        relation.SetupGet(entity => entity.Attributes).Returns(attributes.Object);
        relation.Setup(entity => entity.LoadAttributes(It.IsAny<Collection<string>>()))
            .Callback<Collection<string>>(_ => values["Count"] = 9);

        relation.Object.GetAttributeValueOrDefault<int>("Count", loadAttribute).Should().Be(expected);
        relation.Verify(entity => entity.LoadAttributes(It.IsAny<Collection<string>>()), Times.Exactly(loadAttribute ? 1 : 0));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void AttributeAndRelatedAttributeChecks_LoadAndDetectValues(bool loadAttribute)
    {
        Dictionary<string, object> values = new();
        var instance = new Mock<IEntityInstance>();
        instance.SetupGet(entity => entity.Attributes).Returns(CreateAttributes(values).Object);
        instance.Setup(entity => entity.LoadAttributes(It.IsAny<Collection<string>>()))
            .Callback<Collection<string>>(_ => values["Count"] = 3);
        instance.Object.HasAttributes(new Collection<string> { "Count" }, loadAttribute).Should().Be(loadAttribute);
        instance.Object.LoadAttribute("Count");
        instance.Verify(entity => entity.LoadAttributes(It.IsAny<Collection<string>>()), Times.Exactly(loadAttribute ? 2 : 1));

        Dictionary<string, object> relatedValues = new();
        var version = new Mock<IEntityVersion>();
        version.SetupGet(entity => entity.RelatedAttributes).Returns(CreateAttributes(relatedValues).Object);
        version.Setup(entity => entity.LoadAttributes(It.IsAny<Collection<string>>()))
            .Callback<Collection<string>>(_ => relatedValues["Revision"] = "R3");
        version.Object.HasRelatedAttributes(new Collection<string> { "Revision" }, loadAttribute).Should().Be(loadAttribute);
        version.Object.LoadAttribute("Revision");
        version.Verify(entity => entity.LoadAttributes(It.IsAny<Collection<string>>()), Times.Exactly(loadAttribute ? 2 : 1));
    }

    [Fact]
    public void MergeAttributeCollections_ReturnsEntriesFromBothCollections()
    {
        IAttributeCollection merged = new AttributeCollection { ["one"] = 1 }
            .MergeAttributeCollections(new AttributeCollection { ["two"] = 2 });
        merged.Should().HaveCount(2);
        merged["one"].Should().Be(1);
        merged["two"].Should().Be(2);
    }

    private static Mock<IAttributeCollection> CreateAttributes(Dictionary<string, object> values)
    {
        Mock<IAttributeCollection> attributes = new();
        attributes.Setup(x => x.ContainsKey(It.IsAny<string>())).Returns<string>(values.ContainsKey);
        attributes.Setup(x => x[It.IsAny<string>()]).Returns<string>(key => values[key]);
        attributes.As<IEnumerable<KeyValuePair<string, object>>>()
            .Setup(x => x.GetEnumerator()).Returns(() => values.GetEnumerator());
        return attributes;
    }
}
