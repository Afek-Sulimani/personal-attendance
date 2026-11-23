using Personal.Attendance.Application.Payroll;

namespace Personal.Attendance.Api.Payroll;

public sealed class UnavailableCompanyPayrollSettingsService : ICompanyPayrollSettingsService
{
    public Task<CompanyPayrollConfiguration> GetAsync(
        PayrollActor actor,
        CancellationToken cancellationToken) =>
        Task.FromException<CompanyPayrollConfiguration>(
            new PayrollUnavailableException("Payroll storage is not configured."));

    public Task<CompanyPayrollConfiguration> SaveAsync(
        PayrollActor actor,
        int payrollPeriodStartDay,
        string timeZoneId,
        TimeOnly defaultWorkStartTime,
        TimeOnly defaultWorkEndTime,
        IReadOnlyCollection<DayOfWeek> defaultWorkDays,
        CancellationToken cancellationToken) =>
        Task.FromException<CompanyPayrollConfiguration>(
            new PayrollUnavailableException("Payroll storage is not configured."));
}
