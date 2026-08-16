using System;
using System.Collections.Generic;
using Cmf.Community.IoTCustomAutomationConfiguration.Actions.Actions.Entities;
using Cmf.Community.IoTCustomAutomationConfiguration.Actions.Orchestration;
using Cmf.Community.IoTCustomAutomationConfiguration.Actions.Orchestration.InputObjects;
using Cmf.Foundation.BaseTestsUtils;
using Cmf.Foundation.BusinessObjects;
using Cmf.Foundation.BusinessOrchestration.EntityTypeManagement.OutputObjects;
using Moq;
using Xunit;

namespace Cmf.Community.IoTCustomAutomationConfiguration.Tests.Actions;

public class CustomAutomationCreateRelatedEntityRelationOnConnectIoTEnableTests : ActionBaseTests
{
    [Fact]
    public void DeeActionCode_ForwardsUpdatedEntityType()
    {
        var entityType = new EntityType { Name = "Resource" };
        var orchestration = new Mock<ICustomAutomationConfigurationOrchestration>();
        var serviceProvider = new Mock<IServiceProvider>();
        serviceProvider.Setup(provider => provider.GetService(typeof(ICustomAutomationConfigurationOrchestration))).Returns(orchestration.Object);
        var action = new CustomAutomationCreateRelatedEntityRelationOnConnectIoTEnable();
        var input = new Dictionary<string, object>
        {
            ["ServiceProvider"] = serviceProvider.Object,
            ["FullUpdateEntityTypeOutput"] = new FullUpdateEntityTypeOutput { EntityType = entityType }
        };

        var result = action.DeeActionCode(input);

        Assert.Same(input, result);
        orchestration.Verify(service => service.CustomAutomationCreateRelatedEntityRelationOnConnectIoTEnabled(
            It.Is<CustomAutomationCreateRelatedEntityRelationOnConnectIoTEnabledInput>(request => request.EntityType == entityType)), Times.Once);
    }
}