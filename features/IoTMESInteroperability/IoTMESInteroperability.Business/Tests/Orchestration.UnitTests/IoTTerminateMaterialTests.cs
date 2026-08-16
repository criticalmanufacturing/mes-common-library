using System.Collections.Generic;
using Cmf.Community.IoTMESInteroperability.Actions;
using Cmf.Foundation.BaseTestsUtils;
using Cmf.Navigo.BusinessObjects.Abstractions;
using Cmf.Navigo.BusinessOrchestration.Abstractions;
using Cmf.Navigo.BusinessOrchestration.MaterialManagement.InputObjects;
using Moq;
using Xunit;

namespace Cmf.Community.IoTMESInteroperability.Tests.Actions;

public class IoTTerminateMaterialTests : ActionBaseTests
{
    [Fact]
    public void DeeTestCondition_AlwaysReturnsTrue()
    {
        Assert.True(new IoTTerminateMaterial().DeeTestCondition(new Dictionary<string, object>()));
    }

    [Fact]
    public void DeeActionCode_WhenTerminateMaterialInputIsMissing_DoesNothing()
    {
        var materialOrchestration = new Mock<IMaterialOrchestration>();
        AddMockToActionInput(materialOrchestration);

        var result = new IoTTerminateMaterial().DeeActionCode(ActionInput);

        Assert.Same(ActionInput, result);
        materialOrchestration.Verify(o => o.TerminateMaterial(It.IsAny<TerminateMaterialInput>()), Times.Never);
    }

    [Fact]
    public void DeeActionCode_WhenTerminateMaterialInputIsProvided_LoadsMaterialAndLossReasonThenTerminates()
    {
        var material = new Mock<IMaterial>();
        var lossReason = new Mock<IReason>();
        var terminateMaterialInput = new TerminateMaterialInput
        {
            Material = material.Object,
            LossReason = lossReason.Object
        };

        var materialOrchestration = new Mock<IMaterialOrchestration>();
        AddMockToActionInput(materialOrchestration);
        ActionInput["TerminateMaterialInput"] = terminateMaterialInput;

        new IoTTerminateMaterial().DeeActionCode(ActionInput);

        material.Verify(m => m.Load(), Times.Once);
        lossReason.Verify(r => r.Load(), Times.Once);
        materialOrchestration.Verify(o => o.TerminateMaterial(terminateMaterialInput), Times.Once);
    }
}
