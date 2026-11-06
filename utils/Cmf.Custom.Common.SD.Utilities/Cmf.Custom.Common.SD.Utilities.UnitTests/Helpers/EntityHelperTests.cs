using System.Collections.ObjectModel;
using Cmf.Custom.Common.SD.Utilities.Helpers;
using Cmf.Custom.Common.SD.Utilities.Extensions;
using Cmf.Custom.Common.SD.Utilities.Helpers.Abstractions;
using Cmf.Foundation.BusinessObjects;
using Cmf.Foundation.BusinessObjects.Abstractions;
using Cmf.Foundation.BusinessOrchestration.Abstractions;
using Cmf.Foundation.BusinessOrchestration.ChangeSetManagement.InputObjects;
using Cmf.Foundation.BusinessOrchestration.GenericServiceManagement.InputObjects;
using Cmf.Foundation.Common;
using Cmf.Foundation.Common.Abstractions;
using FluentAssertions;
using Moq;

namespace Cmf.Custom.Common.SD.Utilities.UnitTests.Helpers;

public class EntityHelperTests
{
    private readonly Mock<IEntityFactory> _entityFactory = new();
    private readonly Mock<IGenericServiceOrchestration> _genericServiceOrchestration = new();
    private readonly Mock<IChangeSetOrchestration> _changeSetOrchestration = new();
    private readonly Mock<ITableHelper> _tableHelper = new();

    private EntityHelper CreateHelper() => new(
        _entityFactory.Object,
        _genericServiceOrchestration.Object,
        _changeSetOrchestration.Object,
        _tableHelper.Object);


    [Fact]
    public void CreateChangeSetStructureAndChangeSet_InitializeAndPersistWithRequestedEffectiveMode()
    {
        Mock<IChangeSet> changeSet = new();
        changeSet.SetupAllProperties();
        _entityFactory.Setup(x => x.Create<IChangeSet>()).Returns(changeSet.Object);
        EntityHelper helper = CreateHelper();

        IChangeSet structure = helper.CreateChangeSetStructure();
        structure.Name.Should().StartWith("AutomaticChangeSet_");
        structure.Type.Should().Be("Generic");
        _tableHelper.Verify(x => x.AddValueToLookupTable("ChangeSetType", "Generic", "Generic"), Times.Once);

        IChangeSet created = helper.CreateChangeSet(false);
        created.MakeEffectiveOnApproval.Should().BeFalse();
        changeSet.Verify(x => x.Create(), Times.Once);
    }

    [Fact]
    public void VersionCreationAndApproval_UsesAndApprovesChangeSets()
    {
        Mock<IChangeSet> changeSet = new();
        changeSet.SetupAllProperties();
        _entityFactory.Setup(x => x.Create<IChangeSet>()).Returns(changeSet.Object);
        EntityHelper helper = CreateHelper();
        Mock<IEntityVersion> version = new();
        version.SetupAllProperties();

        helper.CreateNewEntityVersion(version.Object);

        version.Object.ChangeSet.Should().BeSameAs(changeSet.Object);
        _genericServiceOrchestration.Verify(x => x.CreateObjectVersion(It.IsAny<CreateObjectVersionInput>()), Times.Once);
        _changeSetOrchestration.Verify(x => x.RequestChangeSetApproval(It.IsAny<RequestChangeSetApprovalInput>()), Times.Once);

        Mock<IEntityVersion> collectionItem = new();
        collectionItem.SetupAllProperties();
        Mock<IEntityVersionCollection<IEntityVersion>> versions = new();
        versions.As<IEnumerable<IEntityVersion>>().Setup(x => x.GetEnumerator())
            .Returns(new List<IEntityVersion> { collectionItem.Object }.GetEnumerator());
        helper.CreateNewEntitiesVersion(versions.Object, "unused", true, false);
        collectionItem.Object.ChangeSet.Should().BeSameAs(changeSet.Object);
        _genericServiceOrchestration.Verify(x => x.CreateObjectVersions(It.IsAny<CreateObjectVersionsInput>()), Times.Once);
        _changeSetOrchestration.Verify(x => x.RequestChangeSetApproval(It.IsAny<RequestChangeSetApprovalInput>()), Times.Exactly(2));
    }
}
