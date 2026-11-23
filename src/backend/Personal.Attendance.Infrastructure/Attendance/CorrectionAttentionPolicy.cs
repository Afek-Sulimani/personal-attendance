namespace Personal.Attendance.Infrastructure.Attendance;

public static class CorrectionAttentionPolicy
{
    public static bool IsAttentionRequired(DateOnly workDate, DateTimeOffset nowUtc, int periodStartDay, TimeZoneInfo companyZone)
    {
        if (periodStartDay is < 1 or > 31) throw new ArgumentOutOfRangeException(nameof(periodStartDay));
        var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(nowUtc, companyZone).DateTime);
        var currentStart = StartInMonth(today.Year, today.Month, periodStartDay);
        if (today < currentStart)
        {
            var previousMonth = today.AddMonths(-1);
            currentStart = StartInMonth(previousMonth.Year, previousMonth.Month, periodStartDay);
        }
        return workDate < currentStart;
    }

    private static DateOnly StartInMonth(int year, int month, int day) =>
        new(year, month, Math.Min(day, DateTime.DaysInMonth(year, month)));
}
