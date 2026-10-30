using System.Collections.Generic;
using Cmf.Community.IoTCustomAutomationConfiguration.Actions.Actions.Automation;
using Cmf.Foundation.BaseTestsUtils;
using Xunit;

namespace Cmf.Community.IoTCustomAutomationConfiguration.Tests.Actions;

public class CustomAutomationUpdateConfigurationEntitiesTests : ActionBaseTests
{
    [Fact]
    public void DeeTestCondition_AllowsPreUpdateActionGroup()
    {
        var action = new CustomAutomationUpdateConfigurationEntities();
        var input = new Dictionary<string, object>
        {
            ["ActionGroupName"] = "ConnectIoTManagement.ConnectIoTManagementOrchestration.FullUpdateAutomationControllerInstance.Pre"
        };

        var result = action.DeeTestCondition(input);

        Assert.True(result);
    }
}