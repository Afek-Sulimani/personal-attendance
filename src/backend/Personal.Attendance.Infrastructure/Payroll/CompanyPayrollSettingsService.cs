using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Personal.Attendance.Application.Payroll;
using Personal.Attendance.Application.Time;
using Personal.Attendance.Domain.Entities;
using Personal.Attendance.Domain.Enums;
using Personal.Attendance.Infrastructure.Persistence;

namespace Personal.Attendance.Infrastructure.Payroll;

public sealed class CompanyPayrollSettingsService(
    PersonalAttendanceDbContext dbContext,
    TimeProvider timeProvider,
    ICompanyTimeZoneRegistry timeZoneRegistry) : ICompanyPayrollSettingsService
{
    public async Task<CompanyPayrollConfiguration> GetAsync(
        PayrollActor actor,
        CancellationToken cancellationToken)
    {
        await EnsureAccessAsync(actor, cancellationToken);

        try
        {
            var settings = await dbContext.CompanyPayrollSettings
                .AsNoTracking()
                .Include(item => item.DefaultWorkDays)
                .SingleOrDefaultAsync(item => item.CompanyId == actor.CompanyId, cancellationToken);

            return ToResponse(settings, timeZoneRegistry);
        }
        catch (DbException exception)
        {
            throw new PayrollUnavailableException("Company payroll settings could not be loaded.", exception);
        }
    }

    public async Task<CompanyPayrollConfiguration> SaveAsync(
        PayrollActor actor,
        int payrollPeriodStartDay,
        string timeZoneId,
        TimeOnly defaultWorkStartTime,
        TimeOnly defaultWorkEndTime,
        IReadOnlyCollection<DayOfWeek> defaultWorkDays,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(defaultWorkDays);
        if (payrollPeriodStartDay is < 1 or > 31)
        {
            throw new ArgumentOutOfRangeException(nameof(payrollPeriodStartDay));
        }

        await EnsureAccessAsync(actor, cancellationToken);

        string canonicalTimeZoneId;
        try
        {
            canonicalTimeZoneId = timeZoneRegistry.Register(timeZoneId);
        }
        catch (TimeZoneNotFoundException exception)
        {
            throw new ArgumentException("Use a supported time zone ID, such as Asia/Jerusalem.", nameof(timeZoneId), exception);
        }
        catch (InvalidTimeZoneException exception)
        {
            throw new ArgumentException("The selected time zone definition is invalid.", nameof(timeZoneId), exception);
        }

        try
        {
            var settings = await dbContext.CompanyPayrollSettings
                .Include(item => item.DefaultWorkDays)
                .SingleOrDefaultAsync(item => item.CompanyId == actor.CompanyId, cancellationToken);
            var nowUtc = timeProvider.GetUtcNow().ToUniversalTime();

            if (settings is null)
            {
                settings = CompanyPayrollSettings.Create(
                    actor.CompanyId,
                    payrollPeriodStartDay,
                    canonicalTimeZoneId,
                    defaultWorkStartTime,
                    defaultWorkEndTime,
                    defaultWorkDays,
                    actor.MembershipId,
                    nowUtc);
                dbContext.CompanyPayrollSettings.Add(settings);
            }
            else
            {
                dbContext.CompanyDefaultWorkDays.RemoveRange(settings.DefaultWorkDays);
                settings.Update(
                    payrollPeriodStartDay,
                    canonicalTimeZoneId,
                    defaultWorkStartTime,
                    defaultWorkEndTime,
                    defaultWorkDays,
                    actor.MembershipId,
                    nowUtc);
            }

            await dbContext.SaveChangesAsync(cancellationToken);
            return ToResponse(settings, timeZoneRegistry);
        }
        catch (DbUpdateException exception)
        {
            throw new PayrollUnavailableException("Company payroll settings could not be saved.", exception);
        }
        catch (DbException exception)
        {
            throw new PayrollUnavailableException("Company payroll settings could not be saved.", exception);
        }
    }

    private async Task EnsureAccessAsync(PayrollActor actor, CancellationToken cancellationToken)
    {
        if (actor.UserId == Guid.Empty || actor.CompanyId == Guid.Empty || actor.MembershipId == Guid.Empty)
        {
            throw new PayrollAccessDeniedException("The session does not identify a company membership.");
        }

        try
        {
            var hasAccess = await dbContext.CompanyMemberships
                .AsNoTracking()
                .AnyAsync(
                    membership => membership.MembershipId == actor.MembershipId &&
                                  membership.CompanyId == actor.CompanyId &&
                                  membership.UserId == actor.UserId &&
                                  membership.IsActive &&
                                  membership.Company.IsActive &&
                                  membership.Roles.Any(role => role.Role == CompanyRole.Admin ||
                                                               role.Role == CompanyRole.PayrollRepresentative),
                    cancellationToken);

            if (!hasAccess)
            {
                throw new PayrollAccessDeniedException("An Admin or Payroll Representative role is required.");
            }
        }
        catch (DbException exception)
        {
            throw new PayrollUnavailableException("Company payroll access could not be verified.", exception);
        }
    }

    private static CompanyPayrollConfiguration ToResponse(
        CompanyPayrollSettings? settings,
        ICompanyTimeZoneRegistry timeZoneRegistry) => settings is null
        ? new CompanyPayrollConfiguration(false, null, timeZoneRegistry.DefaultTimeZoneId, null, null, [], null)
        : new CompanyPayrollConfiguration(
            true,
            settings.PayrollPeriodStartDay,
            timeZoneRegistry.Register(settings.TimeZoneId),
            settings.DefaultWorkStartTime,
            settings.DefaultWorkEndTime,
            settings.DefaultWorkDays.Select(day => day.DayOfWeek).Order().ToArray(),
            settings.UpdatedAtUtc);
}
