namespace Personal.Attendance.Application.Payroll;

public sealed record MonthlyWorkSchedule(
    Guid EmployeeMembershipId,
    int Year,
    int Month,
    bool IsConfigured,
    IReadOnlyList<DateOnly> WorkingDates);
