using Cmf.Custom.Common.SD.Utilities.Extensions;
using Cmf.Custom.Common.SD.Utilities.Helpers;
using Cmf.Foundation.BusinessObjects.Abstractions;
using Cmf.Navigo.BusinessObjects.Abstractions;
using Cmf.Foundation.BusinessObjects.QueryObject;
using Cmf.Foundation.Common.Abstractions;
using FluentAssertions;
using Moq;
using System.Data;

namespace Cmf.Custom.Common.SD.Utilities.UnitTests.Extensions;

public class ProductExtensionsTests
{
    [Theory]
    [InlineData(null, "kg", "g", "Product")]
    [InlineData("product", null, "g", "FromUnit")]
    [InlineData("product", "kg", null, "ToUnit")]
    public void GetProductConversionFactors_ShouldRejectMissingInputs(string productName, string fromUnit, string toUnit, string parameterName)
    {
        IProduct product = productName == null ? null : Mock.Of<IProduct>();

        Action call = () => product.GetProductConversionFactors(fromUnit, toUnit);

        call.Should().Throw<ArgumentNullException>().Which.ParamName.Should().Be(parameterName);
    }

    [Theory]
    [InlineData("kg", "g", 2.5)]
    [InlineData("g", "kg", 0.4)]
    [InlineData("lb", "g", null)]
    public void GetProductConversionFactors_ShouldReturnDirectInverseOrMissingFactor(string fromUnit, string toUnit, double? expected)
    {
        var dataSet = TestUtilities.CreateDataSet(
            [new("FromUnit", typeof(string)), new("ToUnit", typeof(string)), new("ConversionFactor", typeof(double))],
            table => table.Rows.Add("kg", "g", 2.5d));
        var genericTable = new Mock<IGenericTable>();
        genericTable.SetupGet(t => t.Data).Returns(dataSet.ToNgpDataSet());
        var product = new Mock<IProduct>();
        product.SetupGet(p => p.UnitConversionFactors).Returns(genericTable.Object);
        var actual = product.Object.GetProductConversionFactors(fromUnit, toUnit, loadUnitConversionFactors: true);

        actual.Should().Be(expected.HasValue ? (decimal?)expected.Value : null);
        product.Verify(p => p.LoadUnitConversionFactors(It.IsAny<int>(), It.IsAny<int>()), Times.Once);
    }
}
