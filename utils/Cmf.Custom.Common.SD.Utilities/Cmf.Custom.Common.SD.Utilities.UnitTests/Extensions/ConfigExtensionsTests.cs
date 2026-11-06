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

    [Fact]
    public void GetChildConfigByPath_FindsPathAndRetainsDefaults()
    {
        var child = new Mock<IConfig>();
        child.SetupGet(config => config.Path).Returns("Root/Child/Target");
        var children = new Mock<IConfigCollection>();
        children.SetupGet(collection => collection.Count).Returns(1);
        children.As<IEnumerable<IConfig>>().Setup(collection => collection.GetEnumerator())
            .Returns(() => new[] { child.Object }.AsEnumerable().GetEnumerator());
        var parent = new Mock<IConfig>();
        parent.SetupGet(config => config.ChildConfigs).Returns(children.Object);
        parent.Object.GetChildConfigByPath("Target").Should().BeSameAs(child.Object);
        parent.Object.GetChildConfigByPath("Target", true).Should().BeSameAs(child.Object);
        parent.Verify(config => config.LoadChildConfigs(), Times.Once);
        Action missing = () => parent.Object.GetChildConfigByPath("Missing");
        missing.Should().Throw<CmfBaseException>().WithMessage("*Specified child config does not exist path: Missing");
        typeof(ConfigExtensions).GetMethod(nameof(ConfigExtensions.GetChildConfigByPath))!
            .GetParameters()[2].DefaultValue.Should().Be(false);
        typeof(ConfigExtensions).GetMethod(nameof(ConfigExtensions.GetConfigValue))!
            .GetParameters()[1].DefaultValue.Should().Be(false);
    }
}