using Cmf.Custom.Common.SD.Utilities.Helpers;
using FluentAssertions;

namespace Cmf.Custom.Common.SD.Utilities.UnitTests.Helpers;

public class HashHelperTests
{
    [Fact]
    public void CreateHash()
    {
        // Arrange
        var helper = new HashHelper();

        // Act
        string result = helper.CreateHash("Hello World");

        // Assert
        result.Should().Be("B10A8DB164E0754105B7A99BE72E3FE5");
    }

    [Fact]
    public void CreateHash_SameValueReturnsSameHash()
    {
        // Arrange
        var helper = new HashHelper();

        // Act
        string hash1 = helper.CreateHash("TestValue");
        string hash2 = helper.CreateHash("TestValue");

        // Assert
        hash1.Should().Be(hash2);
    }

    [Fact]
    public void CreateHash_DifferentValuesReturnDifferentHashes()
    {
        // Arrange
        var helper = new HashHelper();

        // Act
        string hash1 = helper.CreateHash("Value1");
        string hash2 = helper.CreateHash("Value2");

        // Assert
        hash1.Should().NotBe(hash2);
    }

    [Fact]
    public void CreateHash_EmptyString()
    {
        // Arrange
        var helper = new HashHelper();

        // Act
        string result = helper.CreateHash(string.Empty);

        // Assert
        result.Should().Be("D41D8CD98F00B204E9800998ECF8427E");
    }
}