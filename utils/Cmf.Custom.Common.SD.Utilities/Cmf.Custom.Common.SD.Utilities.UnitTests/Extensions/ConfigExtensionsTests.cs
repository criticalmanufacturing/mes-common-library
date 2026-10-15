using Cmf.Custom.Common.SD.Utilities.Extensions;
using Cmf.Foundation.Common;
using Cmf.Foundation.Configuration.Abstractions;
using FluentAssertions;
using Moq;
using System.Security;

namespace Cmf.Custom.Common.SD.Utilities.UnitTests.Extensions;

public class ConfigExtensionsTests
{
    [Fact]
    public void GetConfigurationValue_ShouldReturnValue_WhenTypeMatches()
    {
        // Arrange
        var configMock = new Mock<IConfig>();

        configMock
            .Setup(c => c.Value)
            .Returns("MyValue");

        // Act
        string result = configMock.Object.GetConfigurationValue<string>();

        // Assert
        result.Should().Be("MyValue");
    }

    [Fact]
    public void GetConfigurationValue_ShouldThrowArgumentNullException_WhenConfigIsNull()
    {
        // Arrange
        IConfig config = null!;

        // Act
        Action act = () => config.GetConfigurationValue<string>();

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void GetConfigurationValue_ShouldThrowInvalidOperationException_WhenTypeDoesNotMatch()
    {
        // Arrange
        var configMock = new Mock<IConfig>();

        configMock
            .Setup(c => c.Value)
            .Returns(123);

        // Act
        Action act = () => configMock.Object.GetConfigurationValue<string>();

        // Assert
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void GetConfigurationValue_ShouldReturnDefault_WhenTypeDoesNotMatch_AndThrowExceptionIsFalse()
    {
        // Arrange
        var configMock = new Mock<IConfig>();

        configMock
            .Setup(c => c.Value)
            .Returns(123);

        // Act
        string result = configMock.Object.GetConfigurationValue<string>(false);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void GetDecryptedConfigValue_ShouldThrowInvalidObjectTypeException_WhenValueTypeIsNotSecureString()
    {
        // Arrange
        var configMock = new Mock<IConfig>();

        configMock
            .Setup(c => c.Load("MyConfig"));

        configMock
            .Setup(c => c.ValueType)
            .Returns(typeof(string).FullName);

        // Act
        Action act = () => configMock.Object.GetDecryptedConfigValue("MyConfig");

        // Assert
        act.Should().Throw<InvalidObjectTypeCmfException>();
    }

    [Fact]
    public void GetDecryptedConfigValue_ShouldThrowMissingMandatoryConfigException_WhenValueIsEmpty()
    {
        // Arrange
        var configMock = new Mock<IConfig>();

        configMock
            .Setup(c => c.Load("MyConfig"));

        configMock
            .Setup(c => c.ValueType)
            .Returns(typeof(SecureString).FullName);

        configMock
            .Setup(c => c.Value)
            .Returns(string.Empty);

        // Act
        Action act = () => configMock.Object.GetDecryptedConfigValue("MyConfig");

        // Assert
        act.Should().Throw<MissingMandatoryConfigCmfException>();
    }

    [Fact]
    public void GetDecryptedConfigValue_ShouldNotThrow_WhenValueIsEmpty_AndThrowExceptionIsFalse()
    {
        // Arrange
        var configMock = new Mock<IConfig>();

        configMock
            .Setup(c => c.Load("MyConfig"));

        configMock
            .Setup(c => c.ValueType)
            .Returns(typeof(SecureString).FullName);

        configMock
            .Setup(c => c.Value)
            .Returns(string.Empty);

        // This method is ultimately called by the extension.
        configMock
            .Setup(c => c.GetDecryptedConfigValue())
            .Returns(string.Empty);

        // Act
        string result = configMock.Object.GetDecryptedConfigValue("MyConfig", false);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public void GetDecryptedConfigValue_ShouldReturnDecryptedValue()
    {
        // Arrange
        var configMock = new Mock<IConfig>();

        configMock
            .Setup(c => c.Load("MyConfig"));

        configMock
            .Setup(c => c.ValueType)
            .Returns(typeof(SecureString).FullName);

        configMock
            .Setup(c => c.Value)
            .Returns("encrypted");

        configMock
            .Setup(c => c.GetDecryptedConfigValue())
            .Returns("decrypted");

        // Act
        string result = configMock.Object.GetDecryptedConfigValue("MyConfig");

        // Assert
        result.Should().Be("decrypted");
    }
}