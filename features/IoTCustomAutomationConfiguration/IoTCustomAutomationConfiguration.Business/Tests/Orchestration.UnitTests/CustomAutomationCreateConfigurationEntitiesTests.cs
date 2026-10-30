using System;
using System.Collections.Generic;
using Cmf.Community.IoTCustomAutomationConfiguration.Actions.Actions.Automation;
using Cmf.Community.IoTCustomAutomationConfiguration.Actions.Orchestration;
using Cmf.Community.IoTCustomAutomationConfiguration.Actions.Orchestration.InputObjects;
using Cmf.Foundation.BaseTestsUtils;
using Moq;
using Xunit;

namespace Cmf.Community.IoTCustomAutomationConfiguration.Tests.Actions;

public class CustomAutomationCreateConfigurationEntitiesTests : ActionBaseTests
{
    [Fact]
    public void DeeActionCode_ForwardsControllerAndDriversToOrchestration()
    {
        var orchestration = new Mock<ICustomAutomationConfigurationOrchestration>();
        var serviceProvider = CreateServiceProvider(orchestration.Object);
        var action = new CustomAutomationCreateConfigurationEntities();
        var input = new Dictionary<string, object>
        {
            ["ServiceProvider"] = serviceProvider.Object,
            ["AutomationControllerInstance"] = null,
            ["AutomationDriverInstanceCollection"] = null
        };

        var result = action.DeeActionCode(input);

        Assert.Same(input, result);
        orchestration.Verify(service => service.CustomAutomationCreateConfigurationEntities(
            It.Is<CustomAutomationCreateConfigurationEntitiesInput>(request =>
                request.AutomationControllerInstance == null && request.AutomationDriverInstanceCollection == null)), Times.Once);
    }

    private static Mock<IServiceProvider> CreateServiceProvider(ICustomAutomationConfigurationOrchestration orchestration)
    {
        var serviceProvider = new Mock<IServiceProvider>();
        serviceProvider.Setup(provider => provider.GetService(typeof(ICustomAutomationConfigurationOrchestration))).Returns(orchestration);
        return serviceProvider;
    }
}