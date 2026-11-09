using System;
using System.Collections.Generic;
using Cmf.Community.IoTCustomAutomationConfiguration.Actions.Actions.ProcessRules.Baseline.Before;
using Cmf.Foundation.BaseTestsUtils;
using Cmf.Foundation.BusinessObjects.Abstractions;
using Cmf.Foundation.Common.Abstractions;
using Moq;
using Xunit;

namespace Cmf.Community.IoTCustomAutomationConfiguration.Tests.Actions;

public class CustomEnableIoTFlagResourceTests : ActionBaseTests
{
    [Fact]
    public void DeeActionCode_WhenResourceIsNotEnabled_EnablesAndSavesIt()
    {
        var resourceEntityType = new Mock<IEntityType>();
        resourceEntityType.SetupGet(entity => entity.ConnectIoTEnabled).Returns(false);
        var action = new CustomEnableIoTFlagResource();

        action.DeeActionCode(CreateInput(resourceEntityType.Object));

        resourceEntityType.Verify(entity => entity.Load("Resource"), Times.Once);
        resourceEntityType.VerifySet(entity => entity.ConnectIoTEnabled = true, Times.Once);
        resourceEntityType.Verify(entity => entity.Save(), Times.Once);
    }

    [Fact]
    public void DeeActionCode_WhenResourceIsAlreadyEnabled_DoesNotSaveIt()
    {
        var resourceEntityType = new Mock<IEntityType>();
        resourceEntityType.SetupGet(entity => entity.ConnectIoTEnabled).Returns(true);
        var action = new CustomEnableIoTFlagResource();

        action.DeeActionCode(CreateInput(resourceEntityType.Object));

        resourceEntityType.Verify(entity => entity.Load("Resource"), Times.Once);
        resourceEntityType.VerifySet(entity => entity.ConnectIoTEnabled = true, Times.Never);
        resourceEntityType.Verify(entity => entity.Save(), Times.Never);
    }

    private static Dictionary<string, object> CreateInput(IEntityType resourceEntityType)
    {
        var entityFactory = new Mock<IEntityFactory>();
        entityFactory.Setup(factory => factory.Create<IEntityType>()).Returns(resourceEntityType);
        var serviceProvider = new Mock<IServiceProvider>();
        serviceProvider.Setup(provider => provider.GetService(typeof(IEntityFactory))).Returns(entityFactory.Object);
        return new Dictionary<string, object> { ["ServiceProvider"] = serviceProvider.Object };
    }
}