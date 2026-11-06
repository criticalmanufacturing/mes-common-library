using Cmf.Custom.Common.SD.Utilities.Extensions;
using Cmf.Foundation.BusinessObjects;
using Cmf.Foundation.BusinessObjects.Abstractions;
using FluentAssertions;
using Moq;

namespace Cmf.Custom.Common.SD.Utilities.UnitTests.Extensions;

public class LookupTableExtensionsTests
{
    [Fact]
    public void LookupTableExtensions_AddAndFilterValues()
    {
        var values = new LookupTableValueCollection
        {
            Mock.Of<ILookupTableValue>(value => value.Value == "Available" && value.Description == "Existing"),
            Mock.Of<ILookupTableValue>(value => value.Value == "Beta" && value.Description == "Second")
        };
        var table = new Mock<ILookupTable>();
        table.SetupGet(entity => entity.Values).Returns(values);

        table.Object.AddValue("available").Should().BeFalse();
        table.Object.AddValue("NewValue", "Description").Should().BeTrue();
        table.Object.GetLookupTableValues("bETA").Should().ContainSingle().Which.Value.Should().Be("Beta");
        new Mock<ILookupTable>().Object.AddValue("Value").Should().BeFalse();
    }
}
