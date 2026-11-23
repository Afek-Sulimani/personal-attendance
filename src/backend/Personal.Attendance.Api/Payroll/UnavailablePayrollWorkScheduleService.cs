using Personal.Attendance.Application.Payroll;

namespace Personal.Attendance.Api.Payroll;

public sealed class UnavailablePayrollWorkScheduleService : IPayrollWorkScheduleService
{
    public Task<IReadOnlyList<PayrollEmployee>> GetEmployeesAsync(
        PayrollActor actor,
        CancellationToken cancellationToken) =>
        Task.FromException<IReadOnlyList<PayrollEmployee>>(
            new PayrollUnavailableException("Payroll storage is not configured."));

    public Task<MonthlyWorkSchedule> GetScheduleAsync(
        PayrollActor actor,
        Guid employeeMembershipId,
        int year,
        int month,
        CancellationToken cancellationToken) =>
        Task.FromException<MonthlyWorkSchedule>(
            new PayrollUnavailableException("Payroll storage is not configured."));

    public Task<MonthlyWorkSchedule> SaveScheduleAsync(
        PayrollActor actor,
        Guid employeeMembershipId,
        int year,
        int month,
        IReadOnlyCollection<DateOnly> workingDates,
        CancellationToken cancellationToken) =>
        Task.FromException<MonthlyWorkSchedule>(
            new PayrollUnavailableException("Payroll storage is not configured."));
}
