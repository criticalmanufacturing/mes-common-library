using Cmf.Custom.Common.SD.Utilities.Exceptions;
using Cmf.Custom.Common.SD.Utilities.Extensions;
using Cmf.Foundation.Common;
using FluentAssertions;
using System.Text;
using System.Xml;
using Xunit;

namespace Cmf.Custom.Common.SD.Utilities.UnitTests.Extensions;

public class XmlExtensionsTests
{
    public class TestModel
    {
        public string Name { get; set; }
        public int Age { get; set; }
    }

    [Fact]
    public void GetValueByXPath_ShouldReturnInnerText_WhenNodeExists()
    {
        // Arrange
        XmlDocument doc = new();
        doc.LoadXml("<Root><Name>John</Name></Root>");

        // Act
        string result = doc.GetValueByXPath("/Root/Name");

        // Assert
        result.Should().Be("John");
    }

    [Fact]
    public void GetValueByXPath_ShouldReturnAttributeValue_WhenAttributeIsSpecified()
    {
        // Arrange
        XmlDocument doc = new();
        doc.LoadXml("<Root><Person Id=\"123\">John</Person></Root>");

        // Act
        string result = doc.GetValueByXPath("/Root/Person", "Id");

        // Assert
        result.Should().Be("123");
    }

    [Fact]
    public void GetValueByXPath_ShouldReturnNull_WhenAttributeDoesNotExist()
    {
        // Arrange
        XmlDocument doc = new();
        doc.LoadXml("<Root><Person>John</Person></Root>");

        // Act
        string result = doc.GetValueByXPath("/Root/Person", "Id");

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void GetValueByXPath_ShouldThrow_WhenNodeDoesNotExist()
    {
        // Arrange
        XmlDocument doc = new();
        doc.LoadXml("<Root><Name>John</Name></Root>");

        // Act
        Action act = () => doc.GetValueByXPath("/Root/Age");

        // Assert
        act.Should()
            .Throw<SDUtilitiesException>();
    }

    [Fact]
    public void ToXml_ShouldSerializeObject()
    {
        // Arrange
        var model = new TestModel
        {
            Name = "John",
            Age = 30
        };

        // Act
        string xml = model.ToXml();

        // Assert
        xml.Should().Contain("<Name>John</Name>");
        xml.Should().Contain("<Age>30</Age>");
        xml.Should().Contain("<?xml");
    }

    [Fact]
    public void ToXml_ShouldIgnoreNamespaces_WhenRequested()
    {
        // Arrange
        var model = new TestModel
        {
            Name = "John",
            Age = 30
        };

        // Act
        string xml = model.ToXml(ignoreNamespaces: true);

        // Assert
        xml.Should().Contain("<TestModel>");
        xml.Should().Contain("<Name>John</Name>");
        xml.Should().Contain("<Age>30</Age>");
        xml.Should().NotContain("xmlns:xsi");
        xml.Should().NotContain("xmlns:xsd");
    }
}