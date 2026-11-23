namespace Personal.Attendance.Application.Payroll;

public interface ICompanyPayrollSettingsService
{
    Task<CompanyPayrollConfiguration> GetAsync(
        PayrollActor actor,
        CancellationToken cancellationToken);

    Task<CompanyPayrollConfiguration> SaveAsync(
        PayrollActor actor,
        int payrollPeriodStartDay,
        string timeZoneId,
        TimeOnly defaultWorkStartTime,
        TimeOnly defaultWorkEndTime,
        IReadOnlyCollection<DayOfWeek> defaultWorkDays,
        CancellationToken cancellationToken);
}
