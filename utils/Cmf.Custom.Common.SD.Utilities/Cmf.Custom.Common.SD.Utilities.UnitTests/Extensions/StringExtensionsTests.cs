using Cmf.Custom.Common.SD.Utilities.Extensions;
using FluentAssertions;

namespace Cmf.Custom.Common.SD.Utilities.UnitTests.Extensions;

public class StringExtensionsTests
{
    [Theory]
    [InlineData("A,B,C", ",")]
    [InlineData("One;Two;Three", ";")]
    [InlineData("ABC", ",")]
    public void IsStringListValid_ShouldReturnTrue_WhenInputIsValid(
        string value,
        string separator)
    {
        // Act
        bool result = value.IsStringListValid(separator);

        // Assert
        result.Should().BeTrue();
    }

    [Theory]
    [InlineData(null, ",")]
    [InlineData("", ",")]
    [InlineData(" ", ",")]
    [InlineData("A,B,C", null)]
    [InlineData("A,B,C", "")]
    [InlineData("A,B,C", " ")]
    [InlineData(",A,B,C", ",")]
    [InlineData("A,B,C,", ",")]
    [InlineData("A,,C", ",")]
    [InlineData("A, ,C", ",")]
    [InlineData("A|B||C", "|")]
    public void IsStringListValid_ShouldReturnFalse_WhenInputIsInvalid(
        string value,
        string separator)
    {
        // Act
        bool result = value.IsStringListValid(separator);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void CreateHash_ShouldReturnExpectedMd5Hash()
    {
        // Arrange
        const string input = "HelloWorld";

        // Act
        string hash = input.CreateHash();

        // Assert
        hash.Should().Be("68E109F0F40CA72A15E05CC22786F8E6");
    }

    [Fact]
    public void CreateHash_ShouldReturnSameHash_ForSameInput()
    {
        // Arrange
        const string input = "Test123";

        // Act
        string hash1 = input.CreateHash();
        string hash2 = input.CreateHash();

        // Assert
        hash1.Should().Be(hash2);
    }

    [Fact]
    public void CreateHash_ShouldReturnDifferentHashes_ForDifferentInputs()
    {
        // Act
        string hash1 = "Test1".CreateHash();
        string hash2 = "Test2".CreateHash();

        // Assert
        hash1.Should().NotBe(hash2);
    }
}