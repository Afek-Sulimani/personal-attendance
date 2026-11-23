using System.Data;
using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Personal.Attendance.Application.Attendance;
using Personal.Attendance.Application.Time;
using Personal.Attendance.Domain.Entities;
using Personal.Attendance.Domain.Enums;
using Personal.Attendance.Infrastructure.Persistence;

namespace Personal.Attendance.Infrastructure.Attendance;

public sealed class AdminAttendanceService(
    PersonalAttendanceDbContext dbContext,
    ICurrentAttendanceClock attendanceClock,
    ICompanyTimeZoneRegistry timeZoneRegistry) : IAdminAttendanceService
{
    public async Task<AdminAttendanceMonth> GetMonthAsync(
        AdminAttendanceActor actor,
        Guid employeeMembershipId,
        int year,
        int month,
        CancellationToken cancellationToken)
    {
        ValidateActor(actor);
        ValidatePeriod(year, month);

        try
        {
            await EnsureAdminAccessAsync(actor, cancellationToken);
            var employee = await FindEmployeeAsync(actor.CompanyId, employeeMembershipId, cancellationToken);
            var timeZone = await GetCompanyTimeZoneAsync(actor.CompanyId, cancellationToken);
            var firstDate = new DateOnly(year, month, 1);
            var nextMonth = firstDate.AddMonths(1);

            var shifts = await dbContext.WorkShifts
                .AsNoTracking()
                .Where(shift => shift.CompanyId == actor.CompanyId &&
                                shift.EmployeeProfileId == employee.EmployeeProfileId &&
                                shift.WorkDate >= firstDate &&
                                shift.WorkDate < nextMonth)
                .OrderBy(shift => shift.ClockInAtUtc)
                .ToListAsync(cancellationToken);
            var shiftsResponse = shifts.Select(shift => new AdminAttendanceShift(
                    shift.WorkShiftId,
                    shift.WorkDate,
                    employee.DisplayName,
                    employee.EmployeeNumber,
                    ToLocalTime(shift.ClockInAtUtc, shift.ClockInTimeZoneId),
                    shift.ClockInTimeZoneId,
                    shift.ClockInSource.ToString(),
                    shift.ClockOutAtUtc == null
                        ? null
                        : ToLocalTime(shift.ClockOutAtUtc.Value, shift.ClockOutTimeZoneId!),
                    shift.ClockOutTimeZoneId,
                    shift.ClockOutSource == null ? null : shift.ClockOutSource.ToString()))
                .ToArray();

            var changes = await dbContext.AdminAttendanceChangeLogs
                .AsNoTracking()
                .Where(change => change.CompanyId == actor.CompanyId &&
                                 change.EmployeeProfileId == employee.EmployeeProfileId &&
                                 ((change.WorkDate >= firstDate && change.WorkDate < nextMonth) ||
                                  (change.PreviousWorkDate >= firstDate && change.PreviousWorkDate < nextMonth)))
                .OrderByDescending(change => change.ChangedAtUtc)
                .ToListAsync(cancellationToken);
            var changedByMembershipIds = changes.Select(change => change.ChangedByMembershipId).Distinct().ToArray();
            var changedByNames = await dbContext.CompanyMemberships
                .AsNoTracking()
                .Where(membership => membership.CompanyId == actor.CompanyId &&
                                     changedByMembershipIds.Contains(membership.MembershipId))
                .Select(membership => new
                {
                    membership.MembershipId,
                    DisplayName = membership.EmployeeProfile == null
                        ? "Company administrator"
                        : membership.EmployeeProfile.DisplayName
                })
                .ToDictionaryAsync(item => item.MembershipId, item => item.DisplayName, cancellationToken);
            var changesResponse = changes.Select(change => new AdminAttendanceChange(
                    change.AuditId,
                    change.ChangedByMembershipId,
                    changedByNames.GetValueOrDefault(change.ChangedByMembershipId, "Company administrator"),
                    change.PreviousWorkDate,
                    change.WorkDate,
                    employee.DisplayName,
                    employee.EmployeeNumber,
                    ToLocalTime(change.PreviousClockInAtUtc, change.PreviousClockInTimeZoneId),
                    change.PreviousClockInTimeZoneId,
                    change.PreviousClockInSource.ToString(),
                    change.PreviousClockInProvider,
                    change.PreviousClockInProviderSampleUtc,
                    ToLocalTime(change.ClockInAtUtc, change.ClockInTimeZoneId),
                    change.ClockInTimeZoneId,
                    change.ClockInSource.ToString(),
                    change.ClockInProvider,
                    change.ClockInProviderSampleUtc,
                    change.PreviousClockOutAtUtc == null
                        ? null
                        : ToLocalTime(change.PreviousClockOutAtUtc.Value, change.PreviousClockOutTimeZoneId!),
                    change.PreviousClockOutTimeZoneId,
                    change.PreviousClockOutSource?.ToString(),
                    change.PreviousClockOutProvider,
                    change.PreviousClockOutProviderSampleUtc,
                    change.ClockOutAtUtc == null
                        ? null
                        : ToLocalTime(change.ClockOutAtUtc.Value, change.ClockOutTimeZoneId!),
                    change.ClockOutTimeZoneId,
                    change.ClockOutSource?.ToString(),
                    change.ClockOutProvider,
                    change.ClockOutProviderSampleUtc,
                    change.Reason,
                    ToLocalTime(change.ChangedAtUtc, change.ChangeTimeZoneId),
                    change.ChangeTimeZoneId,
                    change.ChangeTimeProvider,
                    change.ChangeProviderSampleUtc)).ToArray();

            return new AdminAttendanceMonth(employeeMembershipId, year, month, shiftsResponse, changesResponse);
        }
        catch (DbException exception)
        {
            throw CreateUnavailableException(exception);
        }
    }

    public async Task<AdminAttendanceChange> UpdateShiftAsync(
        AdminAttendanceActor actor,
        Guid workShiftId,
        DateTime clockInLocalDateTime,
        DateTime? clockOutLocalDateTime,
        string reason,
        CancellationToken cancellationToken)
    {
        ValidateActor(actor);
        if (workShiftId == Guid.Empty) throw new ArgumentException("A work shift is required.", nameof(workShiftId));
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        if (reason.Trim().Length > 1000) throw new ArgumentOutOfRangeException(nameof(reason));

        try
        {
            await EnsureAdminAccessAsync(actor, cancellationToken);
            var timeZone = await GetCompanyTimeZoneAsync(actor.CompanyId, cancellationToken);
            var reading = await GetTimeReadingAsync(timeZone.Id, cancellationToken);

            await using var transaction = await dbContext.Database.BeginTransactionAsync(
                IsolationLevel.Serializable,
                cancellationToken);

            await EnsureAdminAccessAsync(actor, cancellationToken);
            var changedByName = await dbContext.EmployeeProfiles
                .AsNoTracking()
                .Where(profile => profile.CompanyId == actor.CompanyId &&
                                  profile.MembershipId == actor.MembershipId)
                .Select(profile => profile.DisplayName)
                .SingleOrDefaultAsync(cancellationToken) ?? "Company administrator";
            var shift = await dbContext.WorkShifts
                .Include(item => item.EmployeeProfile)
                .SingleOrDefaultAsync(
                    item => item.CompanyId == actor.CompanyId && item.WorkShiftId == workShiftId,
                    cancellationToken);
            if (shift is null)
            {
                throw new AdminAttendanceNotFoundException("The selected attendance shift was not found in this company.");
            }

            if (shift.IsOpen == clockOutLocalDateTime.HasValue)
            {
                throw new AdminAttendanceConflictException(
                    "The correction must keep the current shift state: a closed shift needs a Clock Out time, and an open shift must remain open.");
            }

            var clockInTimeZone = GetTimeZone(shift.ClockInTimeZoneId);
            var clockOutTimeZone = GetTimeZone(shift.ClockOutTimeZoneId ?? shift.ClockInTimeZoneId);
            var clockInAtUtc = ConvertLocalTimeToUtc(clockInLocalDateTime, nameof(clockInLocalDateTime), clockInTimeZone);
            var clockOutAtUtc = clockOutLocalDateTime is { } localClockOut
                ? ConvertLocalTimeToUtc(localClockOut, nameof(clockOutLocalDateTime), clockOutTimeZone)
                : (DateTimeOffset?)null;

            if (clockInAtUtc > reading.UtcTime || clockOutAtUtc > reading.UtcTime)
            {
                throw new AdminAttendanceConflictException("An Admin correction cannot use a future company-local time.");
            }

            var previous = AttendancePunchSnapshot.From(shift);
            if (previous.ClockInAtUtc == clockInAtUtc && previous.ClockOutAtUtc == clockOutAtUtc)
            {
                throw new AdminAttendanceConflictException("Change at least one attendance time before saving.");
            }

            var correctedWorkDate = DateOnly.FromDateTime(
                TimeZoneInfo.ConvertTime(clockInAtUtc, clockInTimeZone).DateTime);
            var otherShiftOnDate = await dbContext.WorkShifts
                .AsNoTracking()
                .AnyAsync(item => item.CompanyId == actor.CompanyId &&
                                  item.EmployeeProfileId == shift.EmployeeProfileId &&
                                  item.WorkShiftId != shift.WorkShiftId &&
                                  item.WorkDate == correctedWorkDate,
                    cancellationToken);
            if (otherShiftOnDate)
            {
                throw new AdminAttendanceConflictException(
                    "Another shift already exists for this employee and work date.");
            }

            shift.ApplyAdminCorrection(clockInAtUtc, clockOutAtUtc, correctedWorkDate);
            var corrected = AttendancePunchSnapshot.From(shift);
            var audit = AdminAttendanceChangeLog.Create(
                shift,
                previous,
                corrected,
                actor.MembershipId,
                reason,
                reading.UtcTime,
                timeZone.Id,
                reading.Provider,
                reading.ProviderSampleUtcTime);

            dbContext.AdminAttendanceChangeLogs.Add(audit);
            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return ToResponse(audit, shift.EmployeeProfile, changedByName);
        }
        catch (AdminAttendanceConflictException)
        {
            throw;
        }
        catch (AdminAttendanceAccessDeniedException)
        {
            throw;
        }
        catch (AdminAttendanceNotFoundException)
        {
            throw;
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new AdminAttendanceConflictException(
                "The shift changed while the correction was being saved. Refresh and try again.");
        }
        catch (ArgumentException exception)
        {
            throw new AdminAttendanceConflictException(exception.Message);
        }
        catch (DbUpdateException exception) when (
            exception.InnerException is Microsoft.Data.SqlClient.SqlException { Number: 2601 or 2627 })
        {
            throw new AdminAttendanceConflictException("A shift already exists for this employee on that work date.");
        }
        catch (DbUpdateException exception)
        {
            throw CreateUnavailableException(exception);
        }
        catch (DbException exception)
        {
            throw CreateUnavailableException(exception);
        }
    }

    private async Task EnsureAdminAccessAsync(AdminAttendanceActor actor, CancellationToken cancellationToken)
    {
        var hasAccess = await dbContext.CompanyMemberships
            .AsNoTracking()
            .AnyAsync(
                membership => membership.MembershipId == actor.MembershipId &&
                              membership.CompanyId == actor.CompanyId &&
                              membership.UserId == actor.UserId &&
                              membership.IsActive &&
                              membership.Company.IsActive &&
                              membership.Roles.Any(role => role.Role == CompanyRole.Admin),
                cancellationToken);

        if (!hasAccess)
        {
            throw new AdminAttendanceAccessDeniedException("An active Admin role in this company is required.");
        }
    }

    private async Task<EmployeeProfile> FindEmployeeAsync(
        Guid companyId,
        Guid employeeMembershipId,
        CancellationToken cancellationToken)
    {
        if (employeeMembershipId == Guid.Empty)
        {
            throw new AdminAttendanceNotFoundException("Select an employee in this company.");
        }

        return await dbContext.EmployeeProfiles
                   .AsNoTracking()
                   .SingleOrDefaultAsync(
                       profile => profile.CompanyId == companyId &&
                                  profile.MembershipId == employeeMembershipId &&
                                  profile.Membership.IsActive,
                       cancellationToken)
               ?? throw new AdminAttendanceNotFoundException("The selected employee is not an active company member.");
    }

    private async Task<ClockReading> GetTimeReadingAsync(string timeZoneId, CancellationToken cancellationToken)
    {
        try
        {
            return await attendanceClock.GetCurrentTimeForAttendanceAsync(timeZoneId, cancellationToken);
        }
        catch (ClockUnavailableException exception)
        {
            throw new AdminAttendanceUnavailableException("A valid external company time sample is required for this correction.", exception);
        }
    }

    private async Task<TimeZoneInfo> GetCompanyTimeZoneAsync(Guid companyId, CancellationToken cancellationToken)
    {
        var configuredTimeZoneId = await dbContext.CompanyPayrollSettings
            .AsNoTracking()
            .Where(settings => settings.CompanyId == companyId)
            .Select(settings => settings.TimeZoneId)
            .SingleOrDefaultAsync(cancellationToken);
        var timeZoneId = configuredTimeZoneId ?? timeZoneRegistry.DefaultTimeZoneId;
        return TimeZoneInfo.FindSystemTimeZoneById(timeZoneRegistry.Register(timeZoneId));
    }

    private static DateTimeOffset ConvertLocalTimeToUtc(
        DateTime localDateTime,
        string parameterName,
        TimeZoneInfo timeZone)
    {
        if (localDateTime == default || localDateTime.Kind != DateTimeKind.Unspecified)
        {
            throw new ArgumentException("Use a local date and time without an offset.", parameterName);
        }
        if (timeZone.IsInvalidTime(localDateTime))
        {
            throw new ArgumentException("The selected local time does not exist because of a daylight-saving transition.", parameterName);
        }
        if (timeZone.IsAmbiguousTime(localDateTime))
        {
            throw new ArgumentException("The selected local time occurs twice because of a daylight-saving transition.", parameterName);
        }

        return new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(localDateTime, timeZone));
    }

    private AdminAttendanceChange ToResponse(
        AdminAttendanceChangeLog change,
        EmployeeProfile employee,
        string changedByName) => new(
        change.AuditId,
        change.ChangedByMembershipId,
        changedByName,
        change.PreviousWorkDate,
        change.WorkDate,
        employee.DisplayName,
        employee.EmployeeNumber,
        ToLocalTime(change.PreviousClockInAtUtc, change.PreviousClockInTimeZoneId),
        change.PreviousClockInTimeZoneId,
        change.PreviousClockInSource.ToString(),
        change.PreviousClockInProvider,
        change.PreviousClockInProviderSampleUtc,
        ToLocalTime(change.ClockInAtUtc, change.ClockInTimeZoneId),
        change.ClockInTimeZoneId,
        change.ClockInSource.ToString(),
        change.ClockInProvider,
        change.ClockInProviderSampleUtc,
        change.PreviousClockOutAtUtc is { } previousClockOut
            ? ToLocalTime(previousClockOut, change.PreviousClockOutTimeZoneId!)
            : null,
        change.PreviousClockOutTimeZoneId,
        change.PreviousClockOutSource?.ToString(),
        change.PreviousClockOutProvider,
        change.PreviousClockOutProviderSampleUtc,
        change.ClockOutAtUtc is { } clockOut
            ? ToLocalTime(clockOut, change.ClockOutTimeZoneId!)
            : null,
        change.ClockOutTimeZoneId,
        change.ClockOutSource?.ToString(),
        change.ClockOutProvider,
        change.ClockOutProviderSampleUtc,
        change.Reason,
        ToLocalTime(change.ChangedAtUtc, change.ChangeTimeZoneId),
        change.ChangeTimeZoneId,
        change.ChangeTimeProvider,
        change.ChangeProviderSampleUtc);

    private TimeZoneInfo GetTimeZone(string timeZoneId) =>
        TimeZoneInfo.FindSystemTimeZoneById(timeZoneRegistry.Register(timeZoneId));

    private DateTimeOffset ToLocalTime(DateTimeOffset value, string timeZoneId) =>
        TimeZoneInfo.ConvertTime(value, GetTimeZone(timeZoneId));

    private static void ValidateActor(AdminAttendanceActor actor)
    {
        ArgumentNullException.ThrowIfNull(actor);
        if (actor.UserId == Guid.Empty || actor.CompanyId == Guid.Empty || actor.MembershipId == Guid.Empty)
        {
            throw new AdminAttendanceAccessDeniedException("The session does not identify an active company Admin.");
        }
    }

    private static void ValidatePeriod(int year, int month)
    {
        if (year is < 2000 or > 9999) throw new ArgumentOutOfRangeException(nameof(year));
        if (month is < 1 or > 12) throw new ArgumentOutOfRangeException(nameof(month));
    }

    private static AdminAttendanceUnavailableException CreateUnavailableException(Exception exception) =>
        new("Admin attendance data could not be read or saved.", exception);
}
