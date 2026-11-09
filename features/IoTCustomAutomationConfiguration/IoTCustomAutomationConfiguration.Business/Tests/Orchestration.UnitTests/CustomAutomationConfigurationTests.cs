using System;
using System.Collections.Generic;
using System.Reflection;
using Cmf.Foundation.BaseTestsUtils;
using Xunit;

namespace Cmf.Community.IoTCustomAutomationConfiguration.Tests.Actions;

public class CustomAutomationConfigurationTests : ActionBaseTests
{
    [Fact]
    public void DeeTestCondition_AlwaysReturnsTrue()
    {
        var actionType = typeof(Cmf.Community.IoTCustomAutomationConfiguration.Actions.DeeDevBase).Assembly.GetType(
            "Cmf.Community.IoTCustomAutomationConfiguration.Actions.Actions.ProcessRules.EntityTypes.CustomAutomationConfiguration");
        var action = Activator.CreateInstance(actionType!);
        var method = actionType!.GetMethod("DeeTestCondition", BindingFlags.Instance | BindingFlags.Public);

        var result = (bool)method!.Invoke(action, new object[] { new Dictionary<string, object>() })!;

        Assert.True(result);
    }
}