using System;
using System.Collections.Generic;
using Cmf.Community.IoTCustomAutomationConfiguration.Actions.Orchestration;
using Cmf.Community.IoTCustomAutomationConfiguration.Actions.Orchestration.InputObjects;
using Cmf.Foundation.BaseTestsUtils;
using Cmf.Foundation.BusinessObjects;
using Cmf.Foundation.BusinessObjects.Abstractions;
using Cmf.Navigo.BusinessObjects.Abstractions;
using Moq;
using Xunit;

namespace Cmf.Community.IoTCustomAutomationConfiguration.Tests.Orchestration;

public class CustomAutomationConfigurationOrchestrationTests : BaseTests
{
    private sealed class TestableCustomAutomationConfigurationOrchestration : CustomAutomationConfigurationOrchestration
    {
        private readonly Queue<TestAutomationConfiguration> _configurations = new();

        protected override void StartMethod(string methodName, params KeyValuePair<string, object>[] parameters) { }

        protected override void EndMethod(params KeyValuePair<string, object>[] parameters) { }

        public void QueueConfiguration(TestAutomationConfiguration configuration)
        {
            _configurations.Enqueue(configuration);
        }

        protected override Type GetAutomationConfigurationEntityType()
        {
            return typeof(TestAutomationConfiguration);
        }

        protected override dynamic CreateAutomationConfiguration(Type automationConfigurationType)
        {
            return (dynamic)_configurations.Dequeue();
        }
    }

    public sealed class TestAutomationConfiguration
    {
        public string LoadedName { get; private set; }

        public IAutomationDriverInstance AutomationDriverInstance { get; set; }

        public bool WasSaved { get; private set; }

        public void Load(string name)
        {
            LoadedName = name;
        }

        public void Save()
        {
            WasSaved = true;
        }
    }

    [Fact]
    public void CustomAutomationUpdateConfigurationEntities_WhenActionGroupIsPre_ClearsAndSavesEachConfiguration()
    {
        // Arrange
        var firstConfiguration = new TestAutomationConfiguration { AutomationDriverInstance = Mock.Of<IAutomationDriverInstance>() };
        var secondConfiguration = new TestAutomationConfiguration { AutomationDriverInstance = Mock.Of<IAutomationDriverInstance>() };
        var orchestration = new TestableCustomAutomationConfigurationOrchestration();
        orchestration.QueueConfiguration(firstConfiguration);
        orchestration.QueueConfiguration(secondConfiguration);
        var input = new CustomAutomationUpdateConfigurationEntitiesInput
        {
            ActionGroupName = "ConnectIoTManagement.ConnectIoTManagementOrchestration.FullUpdateAutomationControllerInstance.Pre",
            AutomationConfigurationsToUpdate = new List<string> { "ConfigurationA", "ConfigurationB" }
        };

        // Act
        var result = orchestration.CustomAutomationUpdateConfigurationEntities(input);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("ConfigurationA", firstConfiguration.LoadedName);
        Assert.Equal("ConfigurationB", secondConfiguration.LoadedName);
        Assert.Null(firstConfiguration.AutomationDriverInstance);
        Assert.Null(secondConfiguration.AutomationDriverInstance);
        Assert.True(firstConfiguration.WasSaved);
        Assert.True(secondConfiguration.WasSaved);
    }

    [Fact]
    public void CustomAutomationUpdateConfigurationEntities_WhenActionGroupIsPost_AssignsAndSavesEachConfiguration()
    {
        // Arrange
        var firstConfiguration = new TestAutomationConfiguration();
        var secondConfiguration = new TestAutomationConfiguration();
        var updatedDriver = Mock.Of<IAutomationDriverInstance>();
        var orchestration = new TestableCustomAutomationConfigurationOrchestration();
        orchestration.QueueConfiguration(firstConfiguration);
        orchestration.QueueConfiguration(secondConfiguration);
        var input = new CustomAutomationUpdateConfigurationEntitiesInput
        {
            ActionGroupName = "ConnectIoTManagement.ConnectIoTManagementOrchestration.FullUpdateAutomationControllerInstance.Post",
            AutomationConfigurationsToUpdate = new List<string> { "ConfigurationA", "ConfigurationB" },
            UpdatedDriverInstance = updatedDriver
        };

        // Act
        var result = orchestration.CustomAutomationUpdateConfigurationEntities(input);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("ConfigurationA", firstConfiguration.LoadedName);
        Assert.Equal("ConfigurationB", secondConfiguration.LoadedName);
        Assert.Same(updatedDriver, firstConfiguration.AutomationDriverInstance);
        Assert.Same(updatedDriver, secondConfiguration.AutomationDriverInstance);
        Assert.True(firstConfiguration.WasSaved);
        Assert.True(secondConfiguration.WasSaved);
    }

    [Fact]
    public void CustomAutomationUpdateConfigurationEntities_WhenConfigurationsAreNull_Throws()
    {
        // Arrange
        var orchestration = new TestableCustomAutomationConfigurationOrchestration();
        var input = new CustomAutomationUpdateConfigurationEntitiesInput
        {
            AutomationConfigurationsToUpdate = null
        };

        // Act
        var exception = Assert.Throws<Exception>(() =>
            orchestration.CustomAutomationUpdateConfigurationEntities(input));

        // Assert
        Assert.Equal("CustomAutomationUpdateConfigurationEntities - Input property AutomationConfigurationsToUpdate is null or empty.", exception.Message);
    }

    [Fact]
    public void CustomAutomationUpdateConfigurationEntities_WhenConfigurationsAreEmpty_Throws()
    {
        // Arrange
        var orchestration = new TestableCustomAutomationConfigurationOrchestration();
        var input = new CustomAutomationUpdateConfigurationEntitiesInput
        {
            AutomationConfigurationsToUpdate = new List<string>()
        };

        // Act
        var exception = Assert.Throws<Exception>(() =>
            orchestration.CustomAutomationUpdateConfigurationEntities(input));

        // Assert
        Assert.Equal("CustomAutomationUpdateConfigurationEntities - Input property AutomationConfigurationsToUpdate is null or empty.", exception.Message);
    }

    [Fact]
    public void CustomAutomationUpdateConfigurationEntities_WhenActionGroupIsInvalid_Throws()
    {
        // Arrange
        var orchestration = new TestableCustomAutomationConfigurationOrchestration();
        var input = new CustomAutomationUpdateConfigurationEntitiesInput
        {
            ActionGroupName = "InvalidActionGroup",
            AutomationConfigurationsToUpdate = new List<string> { "ConfigurationA" }
        };

        // Act
        var exception = Assert.Throws<Exception>(() =>
            orchestration.CustomAutomationUpdateConfigurationEntities(input));

        // Assert
        Assert.Equal("Action group is invalid, expecting ConnectIoTManagement.ConnectIoTManagementOrchestration.FullUpdateAutomationControllerInstance.Pre/Post.", exception.Message);
    }

    [Fact]
    public void CustomAutomationCreateRelatedEntityRelationOnConnectIoTEnabled_WhenEntityTypeIsNotConnectIoTEnabled_ReturnsWithoutCreatingRelation()
    {
        // Arrange
        var orchestration = new TestableCustomAutomationConfigurationOrchestration();
        var entityType = new EntityType
        {
            Name = "Material",
            ConnectIoTEnabled = false
        };
        var input = new CustomAutomationCreateRelatedEntityRelationOnConnectIoTEnabledInput
        {
            EntityType = entityType
        };

        // Act
        var result = orchestration.CustomAutomationCreateRelatedEntityRelationOnConnectIoTEnabled(input);

        // Assert
        Assert.Same(entityType, result.EntityType);
        Assert.Null(result.RelatedEntityTypeCreated);
    }
}