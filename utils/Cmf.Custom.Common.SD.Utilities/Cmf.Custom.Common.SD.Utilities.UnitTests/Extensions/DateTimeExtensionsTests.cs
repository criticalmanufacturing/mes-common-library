using Cmf.Custom.Common.SD.Utilities.Extensions;
using FluentAssertions;
using System.Globalization;

namespace Cmf.Custom.Common.SD.Utilities.UnitTests.Extensions;

public class DateTimeExtensionsTests
{
    [Theory]
    [InlineData("2021-01-01", DayOfWeek.Monday, 53, 2020)]
    [InlineData("2021-01-04", DayOfWeek.Monday, 1, 2021)]
    [InlineData("2020-12-31", DayOfWeek.Monday, 53, 2020)]
    public void IsoWeekAndYear_ShouldFollowTheConfiguredWeekStart(
        string value,
        DayOfWeek weekStartDay,
        int expectedWeek,
        int expectedYear)
    {
        DateTime date = DateTime.Parse(value, CultureInfo.InvariantCulture);

        date.GetIso8601WeekOfYear(weekStartDay).Should().Be(expectedWeek);
        date.GetIso8601Year(weekStartDay).Should().Be(expectedYear);
    }

    [Theory]
    [InlineData("20261104", "", "yyyyMMdd", "2026-11-04")]
    [InlineData("20261104", "2400", "yyyyMMddHHmm", "2026-11-05")]
    [InlineData("not-a-date", "", "yyyyMMdd", null)]
    public void ParsingAndEquality_ShouldHandleBoundaryValues(
        string date,
        string time,
        string format,
        string? expectedDate)
    {
        DateTime? parsed = DateTimeExtensions.ConvertStringToDateTime(date, time, format);
        parsed?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture).Should().Be(expectedDate);

        DateTime first = new(2026, 11, 4, 12, 30, 0, 100);
        DateTime sameSecond = new(2026, 11, 4, 12, 30, 0, 900);
        ((DateTime?)first).AreDateTimesEqual(sameSecond).Should().BeTrue();
        ((DateTime?)first).AreDateTimesEqual(sameSecond, truncateMilliseconds: false).Should().BeFalse();
        ((DateTime?)null).AreDateTimesEqual(null).Should().BeTrue();
        ((DateTime?)null).AreDateTimesEqual(first).Should().BeFalse();
    }
}
