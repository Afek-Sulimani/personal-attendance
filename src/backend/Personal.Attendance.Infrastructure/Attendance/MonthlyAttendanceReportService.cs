using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Personal.Attendance.Application.Attendance;
using Personal.Attendance.Application.Time;
using Personal.Attendance.Domain.Entities;
using Personal.Attendance.Domain.Enums;
using Personal.Attendance.Infrastructure.Persistence;

namespace Personal.Attendance.Infrastructure.Attendance;

public sealed class MonthlyAttendanceReportService(
    PersonalAttendanceDbContext dbContext,
    ICurrentAttendanceClock attendanceClock,
    ICompanyTimeZoneRegistry timeZoneRegistry) : IMonthlyAttendanceReportService
{
    public async Task<MonthlyAttendanceReportView> GetCurrentMineAsync(AttendanceActor actor, CancellationToken cancellationToken)
    {
        try
        {
            var profile = await GetEmployeeProfileAsync(actor, cancellationToken);
            var period = await GetCurrentPayrollPeriodAsync(actor.CompanyId, cancellationToken);
            return await BuildViewAsync(actor.CompanyId, profile, period, cancellationToken);
        }
        catch (MonthlyAttendanceReportAccessDeniedException) { throw; }
        catch (MonthlyAttendanceReportConflictException) { throw; }
        catch (DbException exception) { throw Unavailable(exception); }
    }

    public async Task<MonthlyAttendanceReportView> GetMineAsync(
        AttendanceActor actor,
        int year,
        int month,
        CancellationToken cancellationToken)
    {
        try
        {
            var profile = await GetEmployeeProfileAsync(actor, cancellationToken);
            var period = await GetPeriodAsync(actor.CompanyId, year, month, cancellationToken);
            return await BuildViewAsync(actor.CompanyId, profile, period, cancellationToken);
        }
        catch (MonthlyAttendanceReportAccessDeniedException) { throw; }
        catch (MonthlyAttendanceReportConflictException) { throw; }
        catch (DbException exception) { throw Unavailable(exception); }
    }

    public async Task<IReadOnlyList<MonthlyAttendanceTeamMember>> GetTeamMembersAsync(
        AttendanceActor actor,
        CancellationToken cancellationToken)
    {
        try
        {
            var reviewer = await GetActiveMembershipAsync(actor, cancellationToken);
            var canViewCompany = CanViewCompanyAttendance(reviewer);
            var isTeamLead = reviewer.Roles.Any(role => role.Role == CompanyRole.TeamLead);
            if (!canViewCompany && !isTeamLead)
            {
                throw new MonthlyAttendanceReportAccessDeniedException(
                    "A direct manager or company administration role is required to view employee attendance.");
            }
            var profiles = await dbContext.EmployeeProfiles.AsNoTracking()
                .Include(profile => profile.Membership)
                    .ThenInclude(membership => membership.Roles)
                .Where(profile => profile.CompanyId == actor.CompanyId &&
                                  profile.Membership.IsActive &&
                                  profile.MembershipId != actor.MembershipId &&
                                  (canViewCompany || profile.Membership.ManagerMembershipId == actor.MembershipId))
                .OrderBy(profile => profile.DisplayName)
                .ThenBy(profile => profile.EmployeeNumber)
                .ToListAsync(cancellationToken);

            return profiles.Select(profile => new MonthlyAttendanceTeamMember(
                profile.MembershipId,
                profile.DisplayName,
                profile.EmployeeNumber)).ToArray();
        }
        catch (MonthlyAttendanceReportAccessDeniedException) { throw; }
        catch (DbException exception) { throw Unavailable(exception); }
    }

    public async Task<MonthlyAttendanceReportView> GetCurrentTeamMemberAsync(
        AttendanceActor actor,
        Guid employeeMembershipId,
        CancellationToken cancellationToken)
    {
        try
        {
            var period = await GetCurrentPayrollPeriodAsync(actor.CompanyId, cancellationToken);
            return await GetTeamMemberViewAsync(actor, employeeMembershipId, period, cancellationToken);
        }
        catch (MonthlyAttendanceReportNotFoundException) { throw; }
        catch (MonthlyAttendanceReportAccessDeniedException) { throw; }
        catch (MonthlyAttendanceReportConflictException) { throw; }
        catch (DbException exception) { throw Unavailable(exception); }
    }

    public async Task<MonthlyAttendanceReportView> GetTeamMemberAsync(
        AttendanceActor actor,
        Guid employeeMembershipId,
        int year,
        int month,
        CancellationToken cancellationToken)
    {
        try
        {
            var period = await GetPeriodAsync(actor.CompanyId, year, month, cancellationToken);
            return await GetTeamMemberViewAsync(actor, employeeMembershipId, period, cancellationToken);
        }
        catch (MonthlyAttendanceReportNotFoundException) { throw; }
        catch (MonthlyAttendanceReportAccessDeniedException) { throw; }
        catch (MonthlyAttendanceReportConflictException) { throw; }
        catch (DbException exception) { throw Unavailable(exception); }
    }

    private async Task<MonthlyAttendanceReportView> GetTeamMemberViewAsync(
        AttendanceActor actor,
        Guid employeeMembershipId,
        ReportPeriod period,
        CancellationToken cancellationToken)
    {
        if (employeeMembershipId == Guid.Empty)
            throw new MonthlyAttendanceReportNotFoundException("The employee was not found.");
        var reviewer = await GetActiveMembershipAsync(actor, cancellationToken);
        var profile = await dbContext.EmployeeProfiles
            .Include(item => item.Membership)
                .ThenInclude(membership => membership.Roles)
            .SingleOrDefaultAsync(item => item.CompanyId == actor.CompanyId &&
                                          item.MembershipId == employeeMembershipId &&
                                          item.Membership.IsActive,
                cancellationToken);
        if (profile is null)
            throw new MonthlyAttendanceReportNotFoundException("The employee was not found.");
        if (!CanViewTeamMember(reviewer, profile))
            throw new MonthlyAttendanceReportAccessDeniedException(
                "You are not allowed to view this employee's attendance.");

        return await BuildViewAsync(actor.CompanyId, profile, period, cancellationToken);
    }

    private async Task<MonthlyAttendanceReportView> BuildViewAsync(
        Guid companyId,
        EmployeeProfile profile,
        ReportPeriod period,
        CancellationToken cancellationToken)
    {
        var settings = await dbContext.CompanyPayrollSettings.AsNoTracking()
            .Include(item => item.DefaultWorkDays)
            .SingleOrDefaultAsync(item => item.CompanyId == companyId, cancellationToken);
        var timezoneId = timeZoneRegistry.Register(period.TimeZoneId);
        var zone = TimeZoneInfo.FindSystemTimeZoneById(timezoneId);
        var firstCalendarMonth = new DateOnly(period.Start.Year, period.Start.Month, 1);
        var lastCalendarMonth = new DateOnly(period.End.Year, period.End.Month, 1);
        var schedules = await dbContext.EmployeeMonthlyWorkSchedules.AsNoTracking()
            .Include(schedule => schedule.WorkingDays)
            .Where(schedule => schedule.CompanyId == companyId &&
                               schedule.EmployeeProfileId == profile.EmployeeProfileId &&
                               (schedule.Year > firstCalendarMonth.Year ||
                                (schedule.Year == firstCalendarMonth.Year && schedule.Month >= firstCalendarMonth.Month)) &&
                               (schedule.Year < lastCalendarMonth.Year ||
                                (schedule.Year == lastCalendarMonth.Year && schedule.Month <= lastCalendarMonth.Month)))
            .ToListAsync(cancellationToken);
        var scheduleByMonth = schedules.ToDictionary(
            item => (item.Year, item.Month),
            item => item.WorkingDays.Select(day => day.WorkDate).ToHashSet());
        var defaultDays = settings?.DefaultWorkDays.Select(item => item.DayOfWeek).ToHashSet() ?? [];
        var isScheduleConfigured = settings is not null &&
                                   (defaultDays.Count > 0 || schedules.Count > 0);

        var shifts = await dbContext.WorkShifts.AsNoTracking()
            .Where(shift => shift.CompanyId == companyId &&
                            shift.EmployeeProfileId == profile.EmployeeProfileId &&
                            shift.WorkDate >= period.Start && shift.WorkDate <= period.End)
            .OrderBy(shift => shift.ClockInAtUtc)
            .ToListAsync(cancellationToken);
        var pendingCorrections = await dbContext.AttendanceCorrectionRequests.AsNoTracking()
            .Where(request => request.CompanyId == companyId &&
                                   request.EmployeeProfileId == profile.EmployeeProfileId &&
                                   request.WorkDate >= period.Start && request.WorkDate <= period.End &&
                                   request.Status == AttendanceCorrectionStatus.PendingApproval)
            .ToListAsync(cancellationToken);
        var correctionByDate = pendingCorrections.ToDictionary(request => request.WorkDate);

        var expectedDates = new HashSet<DateOnly>();
        if (isScheduleConfigured)
        {
            for (var date = period.Start; date <= period.End; date = date.AddDays(1))
            {
                if (scheduleByMonth.TryGetValue((date.Year, date.Month), out var monthDays))
                {
                    if (monthDays.Contains(date)) expectedDates.Add(date);
                }
                else if (defaultDays.Contains(date.DayOfWeek))
                {
                    expectedDates.Add(date);
                }
            }
        }

        var days = new List<MonthlyAttendanceReportDay>();
        var missingCount = 0;
        var completedCount = 0;
        var openCount = 0;
        var totalMinutes = 0;
        var pendingMinutesTotal = 0;
        var clockReading = await GetCurrentClockReadingAsync(timezoneId, cancellationToken);
        var nowUtc = clockReading.UtcTime;
        var today = DateOnly.FromDateTime(clockReading.LocalTime.DateTime);
        for (var date = period.Start; date <= period.End; date = date.AddDays(1))
        {
            var dateShifts = shifts.Where(shift => shift.WorkDate == date).ToArray();
            correctionByDate.TryGetValue(date, out var pendingRequest);
            var shiftViews = dateShifts.Select(shift =>
            {
                var shiftZone = TimeZoneInfo.FindSystemTimeZoneById(timeZoneRegistry.Register(shift.ClockInTimeZoneId));
                var effectiveShiftEndUtc = shift.ClockOutAtUtc ??
                                           Min(nowUtc, shift.ClockInAtUtc.AddHours(WorkShift.MaximumDurationHours));
                var workedMinutes = (int)Math.Max(0, Math.Floor((effectiveShiftEndUtc - shift.ClockInAtUtc).TotalMinutes));
                if (pendingRequest?.WorkShiftId != shift.WorkShiftId) totalMinutes += workedMinutes;
                return new MonthlyAttendanceReportShift(
                    shift.WorkShiftId,
                    shift.WorkDate,
                    TimeZoneInfo.ConvertTime(shift.ClockInAtUtc, shiftZone),
                    shift.ClockInTimeZoneId,
                    shift.ClockOutAtUtc is { } outUtc && shift.ClockOutTimeZoneId is { } outZoneId
                        ? TimeZoneInfo.ConvertTime(outUtc, TimeZoneInfo.FindSystemTimeZoneById(timeZoneRegistry.Register(outZoneId)))
                        : null,
                    shift.ClockOutTimeZoneId,
                    workedMinutes,
                    shift.IsOpen,
                    TimeZoneInfo.ConvertTime(shift.ClockInAtUtc, zone),
                    TimeZoneInfo.ConvertTime(effectiveShiftEndUtc, zone),
                    false);
            }).ToArray();

            if (!expectedDates.Contains(date) && pendingRequest is null) continue;
            MonthlyAttendancePendingCorrection? pendingView = null;
            if (pendingRequest is not null)
            {
                var requestZone = TimeZoneInfo.FindSystemTimeZoneById(timeZoneRegistry.Register(pendingRequest.TimeZoneId));
                var requestedInUtc = pendingRequest.RequestedClockInAtUtc ??
                    shifts.FirstOrDefault(shift => shift.WorkShiftId == pendingRequest.WorkShiftId)?.ClockInAtUtc;
                var requestedOutUtc = pendingRequest.RequestedClockOutAtUtc;
                var requestedMinutes = requestedInUtc is { } start && requestedOutUtc is { } end
                    ? (int?)Math.Max(0, Math.Floor((end - start).TotalMinutes))
                    : null;
                if (requestedMinutes is { } reportedMinutes)
                {
                    pendingMinutesTotal += reportedMinutes;
                    totalMinutes += reportedMinutes;
                }
                pendingView = new MonthlyAttendancePendingCorrection(
                    requestedInUtc is { } clockIn ? TimeZoneInfo.ConvertTime(clockIn, requestZone) : null,
                    requestedOutUtc is { } clockOut ? TimeZoneInfo.ConvertTime(clockOut, requestZone) : null,
                    pendingRequest.TimeZoneId,
                    requestedMinutes);
            }
            if (date < today && shiftViews.Length == 0 && pendingView is null) missingCount++;
            var hasOpenShift = dateShifts.Any(shift => shift.IsOpen);
            var status = pendingView is not null
                ? "PendingApproval"
                : hasOpenShift
                ? "OpenShift"
                : shiftViews.Length > 0
                    ? shiftViews.All(shift => shift.IsContinuation) ? "Carryover" : "Complete"
                    : date > today
                        ? "UpcomingWorkday"
                        : date == today
                            ? "TodayNoPunch"
                            : "MissingWorkday";
            if (hasOpenShift) openCount++;
            else if (shiftViews.Length > 0) completedCount++;
            days.Add(new MonthlyAttendanceReportDay(date, expectedDates.Contains(date), status, shiftViews, pendingView));
        }

        return new MonthlyAttendanceReportView(
            profile.MembershipId,
            profile.DisplayName,
            profile.EmployeeNumber,
            period.Start,
            period.End,
            timezoneId,
            isScheduleConfigured,
            expectedDates.Count,
            completedCount,
            openCount,
            missingCount,
            pendingCorrections.Count,
            totalMinutes,
            pendingMinutesTotal,
            days);
    }

    private async Task<EmployeeProfile> GetEmployeeProfileAsync(AttendanceActor actor, CancellationToken cancellationToken)
    {
        ValidateActor(actor);
        var membership = await GetActiveMembershipAsync(actor, cancellationToken);
        var profile = await dbContext.EmployeeProfiles
            .Include(item => item.Membership)
            .SingleOrDefaultAsync(
            item => item.CompanyId == actor.CompanyId &&
                    item.EmployeeProfileId == actor.EmployeeProfileId &&
                    item.MembershipId == actor.MembershipId,
            cancellationToken);
        return profile ?? throw new MonthlyAttendanceReportAccessDeniedException("An active employee profile is required.");
    }

    private async Task<CompanyMembership> GetActiveMembershipAsync(AttendanceActor actor, CancellationToken cancellationToken)
    {
        ValidateActor(actor);
        var membership = await dbContext.CompanyMemberships
            .Include(item => item.Roles)
            .SingleOrDefaultAsync(item => item.CompanyId == actor.CompanyId &&
                                          item.MembershipId == actor.MembershipId &&
                                          item.UserId == actor.UserId && item.IsActive && item.Company.IsActive,
                cancellationToken);
        return membership ?? throw new MonthlyAttendanceReportAccessDeniedException("An active company membership is required.");
    }

    private async Task<ReportPeriod> GetPeriodAsync(Guid companyId, int year, int month, CancellationToken cancellationToken)
    {
        if (year is < 2000 or > 9998 || month is < 1 or > 12)
            throw new MonthlyAttendanceReportConflictException("Choose a valid payroll period start month.");
        var settings = await dbContext.CompanyPayrollSettings.AsNoTracking()
            .SingleOrDefaultAsync(item => item.CompanyId == companyId, cancellationToken);
        var timeZoneId = settings is null ? timeZoneRegistry.DefaultTimeZoneId : timeZoneRegistry.Register(settings.TimeZoneId);
        var startDay = settings?.PayrollPeriodStartDay ?? 1;
        return CreatePayrollPeriod(year, month, startDay, timeZoneId);
    }

    private async Task<ReportPeriod> GetCurrentPayrollPeriodAsync(Guid companyId, CancellationToken cancellationToken)
    {
        var settings = await dbContext.CompanyPayrollSettings.AsNoTracking()
            .SingleOrDefaultAsync(item => item.CompanyId == companyId, cancellationToken);
        var timezoneId = settings is null
            ? timeZoneRegistry.DefaultTimeZoneId
            : timeZoneRegistry.Register(settings.TimeZoneId);
        var reading = await GetCurrentClockReadingAsync(timezoneId, cancellationToken);
        var today = DateOnly.FromDateTime(reading.LocalTime.DateTime);
        var startDay = settings?.PayrollPeriodStartDay ?? 1;
        var thisMonthStart = PayrollPeriodStart(today.Year, today.Month, startDay);
        var start = today >= thisMonthStart ? thisMonthStart : PayrollPeriodStart(today.AddMonths(-1).Year, today.AddMonths(-1).Month, startDay);
        var startOfNextPeriod = PayrollPeriodStart(start.AddMonths(1).Year, start.AddMonths(1).Month, startDay);
        return new ReportPeriod(start, startOfNextPeriod.AddDays(-1), timezoneId);
    }

    private static ReportPeriod CreatePayrollPeriod(int year, int month, int startDay, string timeZoneId)
    {
        var start = PayrollPeriodStart(year, month, startDay);
        var nextMonth = start.AddMonths(1);
        var end = PayrollPeriodStart(nextMonth.Year, nextMonth.Month, startDay).AddDays(-1);
        return new ReportPeriod(start, end, timeZoneId);
    }

    private static DateOnly PayrollPeriodStart(int year, int month, int startDay) =>
        new(year, month, Math.Min(startDay, DateTime.DaysInMonth(year, month)));

    private async Task<ClockReading> GetCurrentClockReadingAsync(
        string timeZoneId,
        CancellationToken cancellationToken)
    {
        try
        {
            return await attendanceClock.GetCurrentTimeAsync(timeZoneId, cancellationToken);
        }
        catch (ClockUnavailableException exception)
        {
            throw new MonthlyAttendanceReportUnavailableException(
                "The synchronized company clock is temporarily unavailable.", exception);
        }
    }

    private static bool CanViewCompanyAttendance(CompanyMembership reviewer) =>
        reviewer.Roles.Any(role => role.Role is CompanyRole.Admin or CompanyRole.Hr or CompanyRole.PayrollRepresentative);

    private static bool CanViewTeamMember(CompanyMembership reviewer, EmployeeProfile profile) =>
        profile.Membership.ManagerMembershipId == reviewer.MembershipId || CanViewCompanyAttendance(reviewer);

    private static DateTimeOffset Min(DateTimeOffset left, DateTimeOffset right) => left <= right ? left : right;

    private static void ValidateActor(AttendanceActor actor)
    {
        ArgumentNullException.ThrowIfNull(actor);
        if (actor.UserId == Guid.Empty || actor.CompanyId == Guid.Empty ||
            actor.MembershipId == Guid.Empty || actor.EmployeeProfileId == Guid.Empty)
            throw new MonthlyAttendanceReportAccessDeniedException("The session does not identify an active company employee.");
    }

    private static MonthlyAttendanceReportUnavailableException Unavailable(Exception exception) =>
        new("The monthly attendance report is temporarily unavailable.", exception);

    private sealed record ReportPeriod(DateOnly Start, DateOnly End, string TimeZoneId);
}
