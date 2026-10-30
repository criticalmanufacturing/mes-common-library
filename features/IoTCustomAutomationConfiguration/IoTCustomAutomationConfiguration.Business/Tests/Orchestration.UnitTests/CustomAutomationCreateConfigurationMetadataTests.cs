using System.Collections.Generic;
using Cmf.Community.IoTCustomAutomationConfiguration.Actions.Actions.Automation;
using Cmf.Foundation.BaseTestsUtils;
using Xunit;

namespace Cmf.Community.IoTCustomAutomationConfiguration.Tests.Actions;

public class CustomAutomationCreateConfigurationMetadataTests : ActionBaseTests
{
    [Theory]
    [InlineData(null, false)]
    [InlineData("BusinessObjects.AutomationProtocolCollection.CreateVersion.Pre", false)]
    [InlineData("BusinessObjects.AutomationProtocolCollection.CreateVersion.Post", true)]
    public void DeeTestCondition_OnlyRunsAfterProtocolVersionCreation(string actionGroupName, bool expected)
    {
        var action = new CustomAutomationCreateConfigurationMetadata();
        var input = new Dictionary<string, object>();
        if (actionGroupName != null)
        {
            input["ActionGroupName"] = actionGroupName;
        }

        var result = action.DeeTestCondition(input);

        Assert.Equal(expected, result);
    }
}