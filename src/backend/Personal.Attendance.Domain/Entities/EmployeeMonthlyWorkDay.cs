namespace Personal.Attendance.Domain.Entities;

public sealed class EmployeeMonthlyWorkDay
{
    private EmployeeMonthlyWorkDay() { }

    public Guid WorkDayId { get; private set; }
    public Guid CompanyId { get; private set; }
    public Guid ScheduleId { get; private set; }
    public DateOnly WorkDate { get; private set; }

    public EmployeeMonthlyWorkSchedule Schedule { get; private set; } = null!;

    internal static EmployeeMonthlyWorkDay Create(Guid companyId, Guid scheduleId, DateOnly workDate) => new()
    {
        WorkDayId = Guid.NewGuid(),
        CompanyId = companyId,
        ScheduleId = scheduleId,
        WorkDate = workDate
    };
}
