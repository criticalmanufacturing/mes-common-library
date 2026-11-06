using System.Globalization;
using System.Text;

namespace Cmf.Custom.Common.SD.Utilities.Extensions;

public static class DateTimeExtensions
{
    private static readonly Calendar Calendar = CultureInfo.InvariantCulture.Calendar;

    public static int GetIso8601WeekOfYear(this DateTime dateTime, DayOfWeek weekStartDay)
    {
        DateTime adjustedDate = GetWeekAdjustedDate(dateTime, weekStartDay);
        DayOfWeek day = Calendar.GetDayOfWeek(adjustedDate);
        if (day >= DayOfWeek.Monday && day <= DayOfWeek.Wednesday)
        {
            adjustedDate = adjustedDate.AddDays(DayOfWeek.Thursday - day);
        }

        return Calendar.GetWeekOfYear(adjustedDate, CalendarWeekRule.FirstFourDayWeek, DayOfWeek.Monday);
    }

    public static int GetIso8601Year(this DateTime dateTime, DayOfWeek weekStartDay)
    {
        DateTime adjustedDate = GetWeekAdjustedDate(dateTime, weekStartDay);
        DayOfWeek day = Calendar.GetDayOfWeek(adjustedDate);
        if (day >= DayOfWeek.Monday && day <= DayOfWeek.Wednesday)
        {
            adjustedDate = adjustedDate.AddDays(DayOfWeek.Thursday - day);
        }

        int year = Calendar.GetYear(adjustedDate);
        if (adjustedDate.Month == 1 || adjustedDate.Month == 12)
        {
            int week = adjustedDate.GetIso8601WeekOfYear(weekStartDay);
            if (adjustedDate.Month == 1 && week >= 52)
            {
                year--;
            }
            else if (adjustedDate.Month == 12 && week == 1)
            {
                year++;
            }
        }

        return year;
    }

    public static DateTime? ConvertStringToDateTime(
        string stringDate,
        string stringTime = "",
        string dateFormat = "yyyyMMdd")
    {
        bool increaseDay = false;
        if (stringTime.StartsWith("24"))
        {
            increaseDay = true;
            stringTime = new StringBuilder(stringTime).Remove(0, 2).Insert(0, "00").ToString();
        }

        return DateTime.TryParseExact(
            stringDate + stringTime,
            dateFormat,
            CultureInfo.InvariantCulture,
            DateTimeStyles.None,
            out DateTime date)
                ? increaseDay ? date.AddDays(1) : date
                : null;
    }

    public static bool AreDateTimesEqual(
        this DateTime? dateTime1,
        DateTime? dateTime2,
        bool truncateMilliseconds = true)
    {
        if (dateTime1 is null || dateTime2 is null)
        {
            return dateTime1 is null && dateTime2 is null;
        }

        DateTime first = truncateMilliseconds ? TruncateMilliseconds(dateTime1.Value) : dateTime1.Value;
        DateTime second = truncateMilliseconds ? TruncateMilliseconds(dateTime2.Value) : dateTime2.Value;
        return first.ToUniversalTime() == second.ToUniversalTime();
    }

    private static DateTime GetWeekAdjustedDate(DateTime dateTime, DayOfWeek weekStartDay)
    {
        int difference = weekStartDay == DayOfWeek.Saturday
            ? 2
            : (int)DayOfWeek.Monday - (int)weekStartDay;
        return dateTime.AddDays(difference);
    }

    private static DateTime TruncateMilliseconds(DateTime dateTime)
    {
        return dateTime.AddTicks(-(dateTime.Ticks % TimeSpan.TicksPerSecond));
    }
}
