namespace Personal.Attendance.Application.Attendance;

public sealed record AttendanceCalendar(
    int Year,
    int Month,
    string TimeZoneId,
    DateOnly TodayLocalDate,
    DateOnly? CurrentPayrollPeriodStartDate,
    DateOnly? CurrentPayrollPeriodEndDate,
    DateTimeOffset CurrentLocalTime,
    bool ScheduleConfigured,
    IReadOnlyList<AttendanceCalendarDay> Days);
