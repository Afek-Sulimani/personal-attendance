namespace Personal.Attendance.Application.Payroll;

public interface IPayrollWorkScheduleService
{
    Task<IReadOnlyList<PayrollEmployee>> GetEmployeesAsync(
        PayrollActor actor,
        CancellationToken cancellationToken);

    Task<MonthlyWorkSchedule> GetScheduleAsync(
        PayrollActor actor,
        Guid employeeMembershipId,
        int year,
        int month,
        CancellationToken cancellationToken);

    Task<MonthlyWorkSchedule> SaveScheduleAsync(
        PayrollActor actor,
        Guid employeeMembershipId,
        int year,
        int month,
        IReadOnlyCollection<DateOnly> workingDates,
        CancellationToken cancellationToken);
}
