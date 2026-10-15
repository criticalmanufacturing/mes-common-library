using Cmf.Custom.Common.SD.Utilities.Extensions;
using Cmf.Foundation.BusinessObjects;
using Cmf.Foundation.BusinessObjects.Abstractions;
using FluentAssertions;
using Moq;
using Newtonsoft.Json;
using System.Text;

namespace Cmf.Custom.Common.SD.Utilities.UnitTests.Extensions;

public class IntegrationEntryExtensionsTests
{
    private class TestMessage
    {
        public string Name { get; set; }
        public int Value { get; set; }
    }

    [Fact]
    public void GetObjectFromIntegrationEntry_ShouldReturnDefault_WhenIntegrationMessageIsNull()
    {
        // Arrange
        var integrationEntryMock = new Mock<IIntegrationEntry>();
        integrationEntryMock.Setup(i => i.IntegrationMessage).Returns((IIntegrationMessage)null);

        // Act
        TestMessage result =
            integrationEntryMock.Object.GetObjectFromIntegrationEntry<TestMessage>();

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void GetObjectFromIntegrationEntry_ShouldDeserializeObject_WhenMessageContainsValidJson()
    {
        // Arrange
        var expected = new TestMessage
        {
            Name = "Material1",
            Value = 100
        };

        var messageMock = new Mock<IIntegrationMessage>();
        messageMock.Setup(m => m.Message)
            .Returns(Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(expected)));

        var integrationEntryMock = new Mock<IIntegrationEntry>();
        integrationEntryMock.Setup(i => i.IntegrationMessage)
            .Returns(messageMock.Object);

        // Act
        TestMessage result =
            integrationEntryMock.Object.GetObjectFromIntegrationEntry<TestMessage>();

        // Assert
        result.Should().NotBeNull();
        result.Name.Should().Be(expected.Name);
        result.Value.Should().Be(expected.Value);
    }

    [Fact]
    public void GetObjectFromIntegrationEntry_ShouldReturnNull_WhenJsonRepresentsNull()
    {
        // Arrange
        var messageMock = new Mock<IIntegrationMessage>();
        messageMock.Setup(m => m.Message)
            .Returns(Encoding.UTF8.GetBytes("null"));

        var integrationEntryMock = new Mock<IIntegrationEntry>();
        integrationEntryMock.Setup(i => i.IntegrationMessage)
            .Returns(messageMock.Object);

        // Act
        TestMessage result =
            integrationEntryMock.Object.GetObjectFromIntegrationEntry<TestMessage>();

        // Assert
        result.Should().BeNull();
    }
}