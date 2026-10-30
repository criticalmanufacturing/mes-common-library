using System.Collections.Generic;
using Cmf.Community.IoTMESInteroperability.Actions;
using Cmf.Foundation.BaseTestsUtils;
using Cmf.Foundation.BusinessObjects.Abstractions;
using Cmf.Foundation.Common.Abstractions;
using Cmf.Navigo.BusinessObjects.Abstractions;
using Cmf.Navigo.BusinessOrchestration.Abstractions;
using Cmf.Navigo.BusinessOrchestration.ResourceManagement.InputObjects;
using Moq;
using Xunit;

namespace Cmf.Community.IoTMESInteroperability.Tests.Actions;

public class IoTAdjustResourceStateTests : ActionBaseTests
{
    [Fact]
    public void DeeTestCondition_AlwaysReturnsTrue()
    {
        var result = new IoTAdjustResourceState().DeeTestCondition(new Dictionary<string, object>());

        Assert.True(result);
    }

    [Fact]
    public void DeeActionCode_WhenRequiredKeysAreMissing_DoesNothing()
    {
        var entityFactory = new Mock<IEntityFactory>();
        AddMockToActionInput(entityFactory);

        new IoTAdjustResourceState().DeeActionCode(ActionInput);

        entityFactory.Verify(f => f.Create<IResource>(), Times.Never);
    }

    [Fact]
    public void DeeActionCode_WhenResourceAlreadyInRequestedState_DoesNotAdjustState()
    {
        var currentState = new Mock<IStateModel>();
        currentState.Setup(s => s.Name).Returns("Running");
        var currentEntityState = new Mock<ICurrentEntityState>();
        currentEntityState.Setup(s => s.StateModel).Returns(currentState.Object);

        var resource = new Mock<IResource>();
        resource.Setup(r => r.CurrentMainState).Returns(currentEntityState.Object);

        var entityFactory = new Mock<IEntityFactory>();
        entityFactory.Setup(f => f.Create<IResource>()).Returns(resource.Object);
        var resourceOrchestration = new Mock<IResourceOrchestration>();

        AddMockToActionInput(entityFactory);
        AddMockToActionInput(resourceOrchestration);
        ActionInput["ResourceName"] = "RES1";
        ActionInput["StateModelStateName"] = "Running";

        new IoTAdjustResourceState().DeeActionCode(ActionInput);

        resource.VerifySet(r => r.Name = "RES1", Times.Once);
        resource.Verify(r => r.Load(), Times.Once);
        resourceOrchestration.Verify(o => o.AdjustResourceState(It.IsAny<AdjustResourceStateInput>()), Times.Never);
    }

    [Fact]
    public void DeeActionCode_WhenResourceIsInADifferentState_AdjustsResourceState()
    {
        var currentState = new Mock<IStateModel>();
        currentState.Setup(s => s.Name).Returns("Idle");
        var currentEntityState = new Mock<ICurrentEntityState>();
        currentEntityState.Setup(s => s.StateModel).Returns(currentState.Object);

        var resource = new Mock<IResource>();
        resource.Setup(r => r.CurrentMainState).Returns(currentEntityState.Object);

        var entityFactory = new Mock<IEntityFactory>();
        entityFactory.Setup(f => f.Create<IResource>()).Returns(resource.Object);
        var resourceOrchestration = new Mock<IResourceOrchestration>();

        AddMockToActionInput(entityFactory);
        AddMockToActionInput(resourceOrchestration);
        ActionInput["ResourceName"] = "RES1";
        ActionInput["StateModelStateName"] = "Running";

        new IoTAdjustResourceState().DeeActionCode(ActionInput);

        resourceOrchestration.Verify(
            o => o.AdjustResourceState(It.Is<AdjustResourceStateInput>(input =>
                input.Resource == resource.Object
                && input.StateModel == currentState.Object
                && input.StateModelStateName == "Running"
                && input.IgnoreLastServiceId == true)),
            Times.Once);
    }
}
