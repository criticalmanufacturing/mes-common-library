using System.Collections.Generic;
using Cmf.Community.IoTCustomAutomationConfiguration.Actions.Actions.NameGenerator;
using Cmf.Foundation.BaseTestsUtils;
using Xunit;

namespace Cmf.Community.IoTCustomAutomationConfiguration.Tests.Actions;

public class CustomAutomationConfigurationNameGeneratorTests : ActionBaseTests
{
    [Fact]
    public void DeeTestCondition_AlwaysReturnsTrue()
    {
        var result = new CustomAutomationConfigurationNameGenerator().DeeTestCondition(new Dictionary<string, object>());

        Assert.True(result);
    }
}