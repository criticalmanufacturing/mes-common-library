using System;
using System.Collections.Generic;
using Cmf.Community.IoTCustomAutomationConfiguration.Actions.Actions.Automation;
using Cmf.Community.IoTCustomAutomationConfiguration.Actions.Common.DataStructures;
using Cmf.Community.IoTCustomAutomationConfiguration.Actions.Orchestration;
using Cmf.Community.IoTCustomAutomationConfiguration.Actions.Orchestration.InputObjects;
using Cmf.Community.IoTCustomAutomationConfiguration.Actions.Orchestration.OutputObjects;
using Cmf.Foundation.BaseTestsUtils;
using Moq;
using Xunit;

namespace Cmf.Community.IoTCustomAutomationConfiguration.Tests.Actions;

public class CustomAutomationRetrieveConfigurationDataTests : ActionBaseTests
{
    [Fact]
    public void DeeActionCode_MapsInputAndPublishesRetrievedData()
    {
        var automationConfigurationData = new AutomationConfigurationData();
        var orchestration = CreateOrchestration(automationConfigurationData);
        var serviceProvider = CreateServiceProvider(orchestration.Object);
        var action = new CustomAutomationRetrieveConfigurationData();
        var input = new Dictionary<string, object>
        {
            ["ServiceProvider"] = serviceProvider.Object,
            ["InstanceName"] = "Driver-01",
            ["InstanceEntityTypeName"] = "Cmf.Custom.EntityTypes.AutomationDriverInstance, Cmf.Custom"
        };

        var result = action.DeeActionCode(input);

        Assert.Same(input, result);
        Assert.Same(automationConfigurationData, input["RetrivedConfigurationData"]);
        orchestration.Verify(service => service.CustomAutomationRetrieveConfiguration(
            It.Is<CustomAutomationRetrieveConfigurationInput>(request =>
                request.RelatedEntityName == "Driver-01" &&
                request.RelatedEntityTypeName == "AutomationDriverInstance" &&
                request.RetrieveControllerConfiguration == true)), Times.Once);
    }

    [Fact]
    public void DeeActionCode_UsesExplicitControllerConfigurationOption()
    {
        var orchestration = CreateOrchestration(new AutomationConfigurationData());
        var serviceProvider = CreateServiceProvider(orchestration.Object);
        var action = new CustomAutomationRetrieveConfigurationData();
        var input = new Dictionary<string, object>
        {
            ["ServiceProvider"] = serviceProvider.Object,
            ["InstanceName"] = "Controller-01",
            ["InstanceEntityTypeName"] = "AutomationControllerInstance",
            ["LoadControlerConfiguration"] = false
        };

        action.DeeActionCode(input);

        orchestration.Verify(service => service.CustomAutomationRetrieveConfiguration(
            It.Is<CustomAutomationRetrieveConfigurationInput>(request =>
                request.RelatedEntityName == "Controller-01" &&
                request.RelatedEntityTypeName == "AutomationControllerInstance" &&
                request.RetrieveControllerConfiguration == false)), Times.Once);
    }

    private static Mock<ICustomAutomationConfigurationOrchestration> CreateOrchestration(AutomationConfigurationData data)
    {
        var orchestration = new Mock<ICustomAutomationConfigurationOrchestration>();
        orchestration.Setup(service => service.CustomAutomationRetrieveConfiguration(It.IsAny<CustomAutomationRetrieveConfigurationInput>()))
            .Returns(new CustomAutomationRetrieveConfigurationOutput { AutomationConfigurationData = data });
        return orchestration;
    }

    private static Mock<IServiceProvider> CreateServiceProvider(ICustomAutomationConfigurationOrchestration orchestration)
    {
        var serviceProvider = new Mock<IServiceProvider>();
        serviceProvider.Setup(provider => provider.GetService(typeof(ICustomAutomationConfigurationOrchestration))).Returns(orchestration);
        return serviceProvider;
    }
}