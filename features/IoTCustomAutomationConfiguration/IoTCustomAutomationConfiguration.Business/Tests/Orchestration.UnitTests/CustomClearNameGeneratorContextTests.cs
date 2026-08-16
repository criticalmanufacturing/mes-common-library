using System.Collections.Generic;
using Cmf.Community.IoTCustomAutomationConfiguration.Actions.Actions.ProcessRules.Baseline.Before;
using Cmf.Foundation.BaseTestsUtils;
using Xunit;

namespace Cmf.Community.IoTCustomAutomationConfiguration.Tests.Actions;

public class CustomClearNameGeneratorContextTests : ActionBaseTests
{
    [Fact]
    public void DeeTestCondition_AlwaysReturnsTrue()
    {
        var result = new CustomClearNameGeneratorContext().DeeTestCondition(new Dictionary<string, object>());

        Assert.True(result);
    }
}