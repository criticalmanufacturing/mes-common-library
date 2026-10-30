using System.Collections.Generic;
using System.Linq;
using Cmf.Community.IoTMESInteroperability.Actions;
using Cmf.Foundation.BaseTestsUtils;
using Cmf.Foundation.Common.Abstractions;
using Cmf.Navigo.BusinessObjects.Abstractions;
using Moq;
using Xunit;

namespace Cmf.Community.IoTMESInteroperability.Tests.Actions;

public class IoTDetachConsumablesTests : ActionBaseTests
{
    [Fact]
    public void DeeTestCondition_AlwaysReturnsTrue()
    {
        Assert.True(new IoTDetachConsumables().DeeTestCondition(new Dictionary<string, object>()));
    }

    [Fact]
    public void DeeActionCode_ForEachReelName_LoadsTheMaterialAndDetachesItFromItsResource()
    {
        var resource = new Mock<IResource>();
        var relation = new Mock<IMaterialResource>();
        relation.Setup(r => r.TargetEntity).Returns(resource.Object);
        // FirstOrDefault() takes the IList<T> fast path (Count/indexer) rather than enumerating, since
        // IMaterialResourceCollection implements IList<IMaterialResource> - GetEnumerator() alone isn't enough.
        var relations = new Mock<IMaterialResourceCollection>();
        relations.As<IList<IMaterialResource>>().Setup(l => l.Count).Returns(1);
        relations.As<IList<IMaterialResource>>().Setup(l => l[0]).Returns(relation.Object);

        var material = new Mock<IMaterial>();
        material.Setup(m => m.MaterialResourceRelations).Returns(relations.Object);

        var entityFactory = new Mock<IEntityFactory>();
        entityFactory.Setup(f => f.Create<IMaterial>()).Returns(material.Object);
        AddMockToActionInput(entityFactory);
        ActionInput["ReelNames"] = new List<string> { "REEL1" };

        new IoTDetachConsumables().DeeActionCode(ActionInput);

        material.VerifySet(m => m.Name = "REEL1", Times.Once);
        material.Verify(m => m.Load(), Times.Once);
        material.Verify(m => m.LoadRelations("MaterialResource"), Times.Once);
        resource.Verify(r => r.Load(), Times.Once);
        resource.Verify(r => r.DetachConsumable(material.Object), Times.Once);
    }

    [Fact]
    public void DeeActionCode_WhenReelHasNoMaterialResourceRelation_ThrowsBecauseFirstOrDefaultIsNotNullChecked()
    {
        // material.MaterialResourceRelations.FirstOrDefault().TargetEntity is dereferenced unconditionally, before
        // the "if (resource != null)" guard below it - a reel with no MaterialResource relation at all (as opposed
        // to one whose TargetEntity happens to be null) hits a NullReferenceException here. Documented and asserted
        // as existing behavior rather than silently "fixed" as a side effect of adding test coverage.
        var relations = new Mock<IMaterialResourceCollection>();
        relations
            .Setup(r => r.GetEnumerator())
            .Returns(Enumerable.Empty<IMaterialResource>().GetEnumerator());

        var material = new Mock<IMaterial>();
        material.Setup(m => m.MaterialResourceRelations).Returns(relations.Object);

        var entityFactory = new Mock<IEntityFactory>();
        entityFactory.Setup(f => f.Create<IMaterial>()).Returns(material.Object);
        AddMockToActionInput(entityFactory);
        ActionInput["ReelNames"] = new List<string> { "REEL1" };

        Assert.Throws<System.NullReferenceException>(() => new IoTDetachConsumables().DeeActionCode(ActionInput));

        material.Verify(m => m.Load(), Times.Once);
    }
}
