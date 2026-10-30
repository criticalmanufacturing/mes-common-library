using System;
using System.Collections.Generic;
using Cmf.Common.CustomActionUtilities.Abstractions;
using Cmf.Community.IoTMESInteroperability;
using Cmf.Community.IoTMESInteroperability.Actions;
using Cmf.Community.IoTMESInteroperability.Orchestration.InputObjects;
using Cmf.Community.IoTMESInteroperability.Orchestration.OutputObjects;
using Cmf.Foundation.BaseTestsUtils;
using Cmf.Foundation.Common.Abstractions;
using Cmf.Navigo.BusinessObjects.Abstractions;
using Moq;
using Xunit;

namespace Cmf.Community.IoTMESInteroperability.Tests.Actions;

public class IoTMaterialOperationTests : ActionBaseTests
{
    [Fact]
    public void DeeTestCondition_WhenOperationKeyIsMissing_ThrowsWithoutALiveCmfHost()
    {
        // GeneralUtilities.ThrowLocalizedException resolves the localization helper from the static
        // ApplicationContext.CurrentServiceProvider (not the DEE's per-call ServiceProvider), which a plain unit
        // test never bootstraps - it surfaces as an ambient-context failure here instead of the intended
        // "InvalidInputs" localized message. Every other DeeTestCondition branch either hits this same call or
        // requires a concrete Material/Resource obtained via an "as" cast that a mock can never satisfy, so this
        // is the one behavior of DeeTestCondition that a unit test can pin down.
        AddMockToActionInput(new Mock<IDEEHelper>());

        Assert.ThrowsAny<Exception>(() => new IoTMaterialOperation().DeeTestCondition(ActionInput));
    }

    [Fact]
    public void DeeActionCode_BuildsInputFromDeeContextAndDelegatesToTheOrchestration()
    {
        var deeHelper = new Mock<IDEEHelper>();
        deeHelper.Setup(h => h.GetContextParameter<IMaterial>("IoTMaterialOperation_Material")).Returns(Mock.Of<IMaterial>());
        deeHelper.Setup(h => h.GetContextParameter<IMaterialCollection>("IoTMaterialOperation_Materials")).Returns((IMaterialCollection)null);
        deeHelper.Setup(h => h.GetContextParameter<IResource>("IoTMaterialOperation_Resource")).Returns(Mock.Of<IResource>());

        var orchestration = new Mock<ICommunityOrchestration>();
        orchestration
            .Setup(o => o.IoTMaterialOperation(It.IsAny<IoTMaterialOperationInput>()))
            .Returns(new IoTMaterialOperationOutput { MaterialName = "MAT1" });

        AddMockToActionInput(deeHelper);
        AddMockToActionInput(orchestration);
        ActionInput["Operation"] = "TrackIn";

        var result = new IoTMaterialOperation().DeeActionCode(ActionInput);

        // The DEE context stores Material/Resource/Materials as concrete Resource/Material/MaterialCollection
        // instances, and casts them back with "as" - Moq's interface proxies aren't instances of those concrete
        // classes, so in this test the cast yields null on all three; that's a limitation of testing through
        // mocked interfaces, not a claim about what happens with the real, concrete-backed context.
        orchestration.Verify(
            o => o.IoTMaterialOperation(It.Is<IoTMaterialOperationInput>(input => input.Operation == "TrackIn")),
            Times.Once);
        Assert.Equal("MAT1", result["MaterialNameOutput"]);
    }

    [Fact]
    public void DeeActionCode_WhenOrchestrationReturnsNoMaterialName_ReturnsEmptyStringOutput()
    {
        var deeHelper = new Mock<IDEEHelper>();
        var orchestration = new Mock<ICommunityOrchestration>();
        orchestration
            .Setup(o => o.IoTMaterialOperation(It.IsAny<IoTMaterialOperationInput>()))
            .Returns(new IoTMaterialOperationOutput { MaterialName = null });

        AddMockToActionInput(deeHelper);
        AddMockToActionInput(orchestration);
        ActionInput["Operation"] = "TrackOut";

        var result = new IoTMaterialOperation().DeeActionCode(ActionInput);

        Assert.Equal(string.Empty, result["MaterialNameOutput"]);
    }
}
