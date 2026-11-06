using Cmf.Custom.Common.SD.Utilities.Extensions;
using Cmf.Foundation.BusinessObjects.Abstractions;
using Cmf.Navigo.BusinessObjects.Abstractions;
using FluentAssertions;
using Moq;

namespace Cmf.Custom.Common.SD.Utilities.UnitTests.Extensions;

public class DataCollectionExtensionsTests
{
    private static IDataCollectionPoint CreatePoint(Cmf.Navigo.BusinessObjects.Abstractions.IParameter parameter, object value, int reading, string sample)
    {
        var point = new Mock<IDataCollectionPoint>();
        point.SetupGet(p => p.Value).Returns(value);
        point.SetupGet(p => p.TargetEntity).Returns(parameter);
        point.SetupGet(p => p.ReadingNumber).Returns(reading);
        point.SetupGet(p => p.SampleId).Returns(sample);
        return point.Object;
    }

[Fact]
    public void GetParameterValue_ShouldReturnMatchingValueForRequestedReadingAndSample()
    {
        var parameter = new Mock<Cmf.Navigo.BusinessObjects.Abstractions.IParameter>();
        parameter.SetupGet(p => p.Name).Returns("Temperature");
        var matchingPoint = CreatePoint(parameter.Object, 23.5m, 2, "Sample-1");
        var otherReading = CreatePoint(parameter.Object, 99m, 3, "Sample-1");
        var otherSample = CreatePoint(parameter.Object, 88m, 2, "Sample-2");
        var latestMatchingPoint = CreatePoint(parameter.Object, 24m, 2, "Sample-1");
        var pointList = new List<IDataCollectionPoint> { matchingPoint, otherReading, otherSample, latestMatchingPoint };
        var points = new Mock<IDataCollectionPointCollection>();
        points.Setup(p => p.GetEnumerator()).Returns(() => pointList.GetEnumerator());
        var instance = new Mock<IDataCollectionInstance>();
        instance.SetupGet(i => i.DataCollectionPoints).Returns(points.Object);

        object result = instance.Object.GetParameterValue( "Temperature", 2, "Sample-1");

        result.Should().Be(24m);
        instance.Object.GetParameterValue( "Temperature").Should().Be(24m);
        instance.Object.GetParameterValue( "Missing").Should().BeNull();
    }

[Fact]
    public void IsDCLimitsViolated_ShouldReturnFalse_WhenInstanceOrLimitsAreMissing()
    {

        Cmf.Custom.Common.SD.Utilities.Extensions.DataCollectionExtensions.IsDCLimitsViolated(null, Mock.Of<IDataCollectionLimitSet>()).Should().BeFalse();
        Cmf.Custom.Common.SD.Utilities.Extensions.DataCollectionExtensions.IsDCLimitsViolated(Mock.Of<IDataCollectionInstance>(), null).Should().BeFalse();

        var parameter = new Mock<Cmf.Navigo.BusinessObjects.Abstractions.IParameter>();
        parameter.SetupGet(p => p.Id).Returns(42);
        parameter.SetupGet(p => p.Name).Returns("Temperature");
        parameter.SetupGet(p => p.DataType).Returns(Cmf.Navigo.BusinessObjects.ParameterDataType.Decimal);
        var point = CreatePoint(parameter.Object, 10m, 1, null);
        var points = new Mock<IDataCollectionPointCollection>();
        points.SetupGet(c => c.Count).Returns(1);
        points.Setup(c => c.GetEnumerator()).Returns(() => new List<IDataCollectionPoint> { point }.GetEnumerator());
        var instance = new Mock<IDataCollectionInstance>();
        instance.SetupGet(i => i.DataCollectionPoints).Returns(points.Object);
        var limit = new Mock<IDataCollectionParameterLimit>();
        limit.SetupGet(l => l.TargetEntity).Returns(parameter.Object);
        limit.SetupGet(l => l.LimitType).Returns(Cmf.Navigo.BusinessObjects.DataCollectionParameterLimitType.Absolute);
        limit.SetupGet(l => l.LowerErrorLimit).Returns(12m);
        var limits = new Mock<IDataCollectionParameterLimitCollection>();
        limits.SetupGet(c => c.Count).Returns(1);
        limits.Setup(c => c.GetEnumerator()).Returns(() => new List<IDataCollectionParameterLimit> { limit.Object }.GetEnumerator());
        var limitSet = new Mock<IDataCollectionLimitSet>();
        limitSet.SetupGet(l => l.DataCollectionParameterLimits).Returns(limits.Object);

        instance.Object.IsDCLimitsViolated(limitSet.Object, "Temperature").Should().BeTrue();
        limit.SetupGet(l => l.LowerErrorLimit).Returns((decimal?)null);
        limit.SetupGet(l => l.LowerWarningLimit).Returns(12m);
        instance.Object.IsDCLimitsViolated(limitSet.Object, includeWarningLimits: false).Should().BeFalse();
        instance.Object.IsDCLimitsViolated(limitSet.Object, includeWarningLimits: true).Should().BeTrue();
        instance.SetupGet(i => i.DataCollectionPoints).Returns((IDataCollectionPointCollection)null);
        instance.Object.IsDCLimitsViolated(limitSet.Object).Should().BeFalse();
    }
}
