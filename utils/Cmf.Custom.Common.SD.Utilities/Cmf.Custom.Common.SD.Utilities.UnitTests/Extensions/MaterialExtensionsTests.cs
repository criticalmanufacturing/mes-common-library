using Cmf.Custom.Common.SD.Utilities.Extensions;
using Cmf.Navigo.BusinessObjects.Abstractions;
using FluentAssertions;
using Moq;

namespace Cmf.Custom.Common.SD.Utilities.UnitTests.Extensions;

public class MaterialExtensionsTests
{
    [Fact]
    public void TotalPrimaryQuantity_IncludesSubMaterialQuantities()
    {
        var material = new Mock<IMaterial>();
        material.SetupGet(item => item.PrimaryQuantity).Returns(4m);
        material.SetupGet(item => item.SubMaterialsPrimaryQuantity).Returns(7m);
        material.Object.TotalPrimaryQuantity().Should().Be(11m);
    }
}
