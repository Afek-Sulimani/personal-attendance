using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Personal.Attendance.Application.Payroll;
using Personal.Attendance.Domain.Entities;
using Personal.Attendance.Domain.Enums;
using Personal.Attendance.Infrastructure.Persistence;

namespace Personal.Attendance.Infrastructure.Payroll;

public sealed class PayrollWorkScheduleService(
    PersonalAttendanceDbContext dbContext,
    TimeProvider timeProvider) : IPayrollWorkScheduleService
{
    public async Task<IReadOnlyList<PayrollEmployee>> GetEmployeesAsync(
        PayrollActor actor,
        CancellationToken cancellationToken)
    {
        await EnsurePayrollAccessAsync(actor, cancellationToken);
        try
        {
            return await dbContext.EmployeeProfiles
                .AsNoTracking()
                .Where(profile => profile.CompanyId == actor.CompanyId && profile.Membership.IsActive)
                .OrderBy(profile => profile.DisplayName)
                .Select(profile => new PayrollEmployee(
                    profile.MembershipId,
                    profile.DisplayName,
                    profile.EmployeeNumber))
                .ToListAsync(cancellationToken);
        }
        catch (DbException exception)
        {
            throw new PayrollUnavailableException("The employee list is temporarily unavailable.", exception);
        }
    }

    public async Task<MonthlyWorkSchedule> GetScheduleAsync(
        PayrollActor actor,
        Guid employeeMembershipId,
        int year,
        int month,
        CancellationToken cancellationToken)
    {
        ValidatePeriod(year, month);
        await EnsurePayrollAccessAsync(actor, cancellationToken);

        try
        {
            var employeeProfile = await FindActiveProfileAsync(actor.CompanyId, employeeMembershipId, cancellationToken);
            var schedule = await dbContext.EmployeeMonthlyWorkSchedules
                .AsNoTracking()
                .Include(item => item.WorkingDays)
                .SingleOrDefaultAsync(
                    item => item.CompanyId == actor.CompanyId &&
                            item.EmployeeProfileId == employeeProfile.EmployeeProfileId &&
                            item.Year == year && item.Month == month,
                    cancellationToken);

            if (schedule is not null)
            {
                return ToResponse(employeeMembershipId, year, month, schedule);
            }

            var defaultDays = await dbContext.CompanyDefaultWorkDays
                .AsNoTracking()
                .Where(day => day.CompanyId == actor.CompanyId)
                .Select(day => day.DayOfWeek)
                .ToArrayAsync(cancellationToken);
            if (defaultDays.Length == 0)
            {
                return ToResponse(employeeMembershipId, year, month, null);
            }

            var defaultDaySet = defaultDays.ToHashSet();
            var daysInMonth = DateTime.DaysInMonth(year, month);
            var defaultDates = Enumerable.Range(1, daysInMonth)
                .Select(dayNumber => new DateOnly(year, month, dayNumber))
                .Where(date => defaultDaySet.Contains(date.DayOfWeek))
                .ToArray();

            return new MonthlyWorkSchedule(employeeMembershipId, year, month, true, defaultDates);
        }
        catch (DbException exception)
        {
            throw new PayrollUnavailableException("The work schedule is temporarily unavailable.", exception);
        }
    }

    public async Task<MonthlyWorkSchedule> SaveScheduleAsync(
        PayrollActor actor,
        Guid employeeMembershipId,
        int year,
        int month,
        IReadOnlyCollection<DateOnly> workingDates,
        CancellationToken cancellationToken)
    {
        ValidatePeriod(year, month);
        ArgumentNullException.ThrowIfNull(workingDates);
        if (workingDates.Any(date => date.Year != year || date.Month != month) ||
            workingDates.Distinct().Count() != workingDates.Count)
        {
            throw new ArgumentException("Work dates must be unique dates inside the selected month.", nameof(workingDates));
        }

        await EnsurePayrollAccessAsync(actor, cancellationToken);
        try
        {
            var employeeProfile = await FindActiveProfileAsync(actor.CompanyId, employeeMembershipId, cancellationToken);
            var nowUtc = timeProvider.GetUtcNow();
            var schedule = await dbContext.EmployeeMonthlyWorkSchedules
                .Include(item => item.WorkingDays)
                .SingleOrDefaultAsync(
                    item => item.CompanyId == actor.CompanyId &&
                            item.EmployeeProfileId == employeeProfile.EmployeeProfileId &&
                            item.Year == year && item.Month == month,
                    cancellationToken);

            if (schedule is null)
            {
                schedule = EmployeeMonthlyWorkSchedule.Create(
                    actor.CompanyId,
                    employeeProfile.EmployeeProfileId,
                    year,
                    month,
                    actor.MembershipId,
                    workingDates,
                    nowUtc);
                dbContext.EmployeeMonthlyWorkSchedules.Add(schedule);
            }
            else
            {
                dbContext.EmployeeMonthlyWorkDays.RemoveRange(schedule.WorkingDays);
                schedule.ReplaceWorkingDates(workingDates, nowUtc);
            }

            await dbContext.SaveChangesAsync(cancellationToken);
            return ToResponse(employeeMembershipId, year, month, schedule);
        }
        catch (DbUpdateException exception)
        {
            throw new PayrollUnavailableException("The work schedule could not be saved.", exception);
        }
        catch (DbException exception)
        {
            throw new PayrollUnavailableException("The work schedule could not be saved.", exception);
        }
    }

    private async Task EnsurePayrollAccessAsync(PayrollActor actor, CancellationToken cancellationToken)
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
                throw new PayrollAccessDeniedException("An active payroll representative role is required.");
            }
        }
        catch (DbException exception)
        {
            throw new PayrollUnavailableException("Payroll access could not be verified.", exception);
        }
    }

    private async Task<EmployeeProfile> FindActiveProfileAsync(
        Guid companyId,
        Guid employeeMembershipId,
        CancellationToken cancellationToken)
    {
        if (employeeMembershipId == Guid.Empty)
        {
            throw new PayrollEmployeeNotFoundException("The selected employee was not found.");
        }

        var employeeProfile = await dbContext.EmployeeProfiles
            .SingleOrDefaultAsync(
                profile => profile.CompanyId == companyId &&
                           profile.MembershipId == employeeMembershipId &&
                           profile.Membership.IsActive,
                cancellationToken);

        return employeeProfile ?? throw new PayrollEmployeeNotFoundException(
            "The selected employee is not an active member of this company.");
    }

    private static MonthlyWorkSchedule ToResponse(
        Guid employeeMembershipId,
        int year,
        int month,
        EmployeeMonthlyWorkSchedule? schedule) => new(
        employeeMembershipId,
        year,
        month,
        schedule is not null,
        schedule?.WorkingDays
            .Select(day => day.WorkDate)
            .Order()
            .ToArray() ?? []);

    private static void ValidatePeriod(int year, int month)
    {
        if (year is < 2000 or > 9999) throw new ArgumentOutOfRangeException(nameof(year));
        if (month is < 1 or > 12) throw new ArgumentOutOfRangeException(nameof(month));
    }
}
