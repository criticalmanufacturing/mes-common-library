using System;
using System.Collections.Generic;
using Cmf.Community.IoTMESInteroperability.Actions;
using Cmf.Foundation.BaseTestsUtils;
using Cmf.Foundation.Common.Abstractions;
using Cmf.Navigo.BusinessObjects.Abstractions;
using Moq;
using Xunit;

namespace Cmf.Community.IoTMESInteroperability.Tests.Actions;

public class IoTCheckGetMaterialsFromPreviousSubResourceTests : ActionBaseTests
{
    [Fact]
    public void DeeTestCondition_AlwaysReturnsTrue()
    {
        var result = new IoTCheckGetMaterialsFromPreviousSubResource().DeeTestCondition(new Dictionary<string, object>());

        Assert.True(result);
    }

    [Fact]
    public void DeeActionCode_RequiresALiveCmfHost_SoItThrowsOutsideOfOne()
    {
        // IoTUtilities.GetMaterialsFromPreviousSubResource (Cmf.Community.IoTMESInteroperability.Utilities) resolves
        // its own collaborators from the static ApplicationContext.CurrentServiceProvider instead of the DEE's
        // per-call ServiceProvider, so it cannot be satisfied by ActionBaseTests' mocked provider. This asserts
        // the resource is still resolved correctly through the mockable, DEE-provided IEntityFactory first.
        var resource = new Mock<IResource>();
        var entityFactory = new Mock<IEntityFactory>();
        entityFactory.Setup(f => f.Create<IResource>()).Returns(resource.Object);
        AddMockToActionInput(entityFactory);
        ActionInput["ResourceName"] = "RES1";

        Assert.ThrowsAny<Exception>(() => new IoTCheckGetMaterialsFromPreviousSubResource().DeeActionCode(ActionInput));

        resource.VerifySet(r => r.Name = "RES1", Times.Once);
        resource.Verify(r => r.Load(), Times.Once);
    }
}
