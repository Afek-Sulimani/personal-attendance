namespace Personal.Attendance.Application.Payroll;

public sealed record CompanyPayrollConfiguration(
    bool IsConfigured,
    int? PayrollPeriodStartDay,
    string TimeZoneId,
    TimeOnly? DefaultWorkStartTime,
    TimeOnly? DefaultWorkEndTime,
    IReadOnlyList<DayOfWeek> DefaultWorkDays,
    DateTimeOffset? UpdatedAtUtc);
