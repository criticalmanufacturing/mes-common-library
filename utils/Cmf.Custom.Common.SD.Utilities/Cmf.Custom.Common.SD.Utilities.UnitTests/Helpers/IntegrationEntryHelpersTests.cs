using Cmf.Custom.Common.SD.Utilities.Helpers;
using Cmf.Foundation.BusinessObjects.Abstractions;
using Cmf.Foundation.BusinessOrchestration.Abstractions;
using Cmf.Foundation.BusinessOrchestration.GenericServiceManagement.InputObjects;
using Cmf.Foundation.Common;
using Cmf.Foundation.Common.Abstractions;
using FluentAssertions;
using Moq;

namespace Cmf.Custom.Common.SD.Utilities.UnitTests.Helpers;

public class IntegrationEntryHelperTests
{
    private readonly Mock<IEntityFactory> _entityFactoryMock = new();
    private readonly Mock<IGenericServiceOrchestration> _genericServiceOrchestrationMock = new();

    private readonly Mock<IIntegrationEntry> _integrationEntryMock = new();
    private readonly Mock<IIntegrationMessage> _integrationMessageMock = new();

    public IntegrationEntryHelperTests()
    {
        _integrationEntryMock.SetupAllProperties();
        _integrationMessageMock.SetupAllProperties();

        _entityFactoryMock
            .Setup(f => f.Create<IIntegrationEntry>())
            .Returns(_integrationEntryMock.Object);

        _entityFactoryMock
            .Setup(f => f.Create<IIntegrationMessage>())
            .Returns(_integrationMessageMock.Object);
    }

    [Fact]
    public void CreateIntegrationEntry()
    {
        // Arrange
        var helper = new IntegrationEntryHelper(
            _entityFactoryMock.Object,
            _genericServiceOrchestrationMock.Object);

        var payload = new
        {
            Id = 100,
            Name = "Test"
        };

        // Act
        IIntegrationEntry result = helper.CreateIntegrationEntry(
            "Entry1",
            "MES",
            "SAP",
            "ProductionOrder",
            "OrderCreated",
            payload);

        // Assert
        result.Should().NotBeNull();

        result.Name.Should().Be("Entry1");
        result.SourceSystem.Should().Be("MES");
        result.TargetSystem.Should().Be("SAP");
        result.MessageType.Should().Be("ProductionOrder");
        result.EventName.Should().Be("OrderCreated");

        result.IsRetriable.Should().BeTrue();
        result.NumberOfRetries.Should().Be(0);
        result.SystemState.Should().Be(IntegrationEntrySystemState.Received);

        result.IntegrationMessage.Should().NotBeNull();
        result.IntegrationMessage.Message.Should().NotBeNull();

        _genericServiceOrchestrationMock.Verify(
            g => g.CreateObject(
                It.Is<CreateObjectInput>(
                    input => input.Object == _integrationEntryMock.Object)),
            Times.Once);
    }

    [Fact]
    public void CreateIntegrationEntry_WithParent()
    {
        // Arrange
        var parent = new Mock<IIntegrationEntry>().Object;

        var helper = new IntegrationEntryHelper(
            _entityFactoryMock.Object,
            _genericServiceOrchestrationMock.Object);

        // Act
        IIntegrationEntry result = helper.CreateIntegrationEntry(
            "Entry1",
            "MES",
            "SAP",
            "ProductionOrder",
            "OrderCreated",
            new { Id = 1 },
            parent);

        // Assert
        result.ParentIntegrationEntry.Should().Be(parent);
    }

    [Fact]
    public void CreateIntegrationEntry_WithoutCreate()
    {
        // Arrange
        var helper = new IntegrationEntryHelper(
            _entityFactoryMock.Object,
            _genericServiceOrchestrationMock.Object);

        // Act
        helper.CreateIntegrationEntry(
            "Entry1",
            "MES",
            "SAP",
            "ProductionOrder",
            "OrderCreated",
            new { Id = 1 },
            null,
            false);

        // Assert
        _genericServiceOrchestrationMock.Verify(
            g => g.CreateObject(It.IsAny<CreateObjectInput>()),
            Times.Never);
    }
}