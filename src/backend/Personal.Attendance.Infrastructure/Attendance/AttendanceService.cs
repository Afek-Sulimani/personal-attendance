using System.Data.Common;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Personal.Attendance.Application.Attendance;
using Personal.Attendance.Application.Time;
using Personal.Attendance.Domain.Entities;
using Personal.Attendance.Domain.Enums;
using Personal.Attendance.Infrastructure.Persistence;

namespace Personal.Attendance.Infrastructure.Attendance;

public sealed class AttendanceService(
    PersonalAttendanceDbContext dbContext,
    ICurrentAttendanceClock attendanceClock,
    ICompanyTimeZoneRegistry timeZoneRegistry) : IAttendanceService
{
    private const string OpenShiftIndexName =
        "IX_ATTENDANCE_WORK_SHIFTS_CompanyId_EmployeeProfileId";

    public async Task<AttendanceCalendar> GetCalendarAsync(
        AttendanceActor actor,
        int? year,
        int? month,
        CancellationToken cancellationToken)
    {
        // Fail fast on a partial payroll-period query; the calendar accepts either both date parts or neither.
        ArgumentNullException.ThrowIfNull(actor);
        if (year.HasValue != month.HasValue)
        {
            throw new ArgumentException("Year and month must be provided together.");
        }

        try
        {
            // Recheck the active company membership in SQL instead of trusting the cookie alone.
            await EnsureActiveMembershipAsync(actor, cancellationToken);

            // Load only settings needed to choose the company's zone and current payroll period.
            var payrollSettings = await dbContext.CompanyPayrollSettings
                .AsNoTracking()
                .Where(settings => settings.CompanyId == actor.CompanyId)
                .Select(settings => new { settings.TimeZoneId, settings.PayrollPeriodStartDay })
                .SingleOrDefaultAsync(cancellationToken);
            // Register a company-specific zone so the hosted service can refresh its clock on later timer ticks.
            var timeZoneId = timeZoneRegistry.Register(
                payrollSettings?.TimeZoneId ?? timeZoneRegistry.DefaultTimeZoneId);
            var timeZone = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
            // Derive the displayed company date from the external-time-backed clock, not from the server date.
            var reading = await GetTimeReadingAsync(timeZone.Id, cancellationToken);
            var todayLocalDate = DateOnly.FromDateTime(reading.LocalTime.DateTime);
            var selectedYear = year ?? todayLocalDate.Year;
            var selectedMonth = month ?? todayLocalDate.Month;
            ValidateCalendarMonth(selectedYear, selectedMonth);

            // A company without payroll settings has no configured payroll-period boundaries to display.
            DateOnly? currentPayrollPeriodStartDate = null;
            DateOnly? currentPayrollPeriodEndDate = null;
            if (payrollSettings is not null)
            {
                (currentPayrollPeriodStartDate, currentPayrollPeriodEndDate) = GetCurrentPayrollPeriod(
                    todayLocalDate,
                    payrollSettings.PayrollPeriodStartDay);
            }

            // Resolve the employee's monthly schedule first, falling back to company default weekdays if absent.
            var (scheduleConfigured, configuredWorkDates) = await GetConfiguredWorkDatesAsync(
                actor.CompanyId,
                actor.EmployeeProfileId,
                selectedYear,
                selectedMonth,
                cancellationToken);

            // Query shifts by the Clock In work date so an overnight shift stays on its starting day.
            var monthStartDate = new DateOnly(selectedYear, selectedMonth, 1);
            var nextMonthStartDate = monthStartDate.AddMonths(1);
            var shifts = await dbContext.WorkShifts
                .AsNoTracking()
                .Where(shift => shift.CompanyId == actor.CompanyId &&
                                shift.EmployeeProfileId == actor.EmployeeProfileId &&
                                shift.WorkDate >= monthStartDate &&
                                shift.WorkDate < nextMonthStartDate)
                .ToListAsync(cancellationToken);
            // Index loaded records by work date so building each calendar cell does not issue another query.
            var shiftsByDate = shifts
                .GroupBy(shift => shift.WorkDate)
                .ToDictionary(group => group.Key, group => group.OrderBy(item => item.ClockInAtUtc).ToArray());
            // Keep pending and rejected corrections for display, newest first so only the latest request wins.
            var correctionsByDate = await dbContext.AttendanceCorrectionRequests
                .AsNoTracking()
                .Where(request => request.CompanyId == actor.CompanyId &&
                                  request.EmployeeProfileId == actor.EmployeeProfileId &&
                                  request.WorkDate >= new DateOnly(selectedYear, selectedMonth, 1) &&
                                  request.WorkDate < new DateOnly(selectedYear, selectedMonth, 1).AddMonths(1) &&
                                  (request.Status == AttendanceCorrectionStatus.PendingApproval ||
                                   request.Status == AttendanceCorrectionStatus.Rejected))
                .OrderByDescending(request => request.SubmittedAtUtc)
                .ToListAsync(cancellationToken);
            // Reduce correction history to one latest request per company-local work date.
            var latestCorrectionByDate = correctionsByDate
                .GroupBy(request => request.WorkDate)
                .ToDictionary(group => group.Key, group => group.First());

            // Build a display row for every calendar date; the React view decides which non-workdays to omit.
            var daysInMonth = DateTime.DaysInMonth(selectedYear, selectedMonth);
            var days = new List<AttendanceCalendarDay>(daysInMonth);
            for (var dayNumber = 1; dayNumber <= daysInMonth; dayNumber++)
            {
                var workDate = new DateOnly(selectedYear, selectedMonth, dayNumber);
                var isWorkDay = scheduleConfigured ? configuredWorkDates.Contains(workDate) : (bool?)null;
                shiftsByDate.TryGetValue(workDate, out var workShifts);
                var shift = workShifts?.FirstOrDefault();
                latestCorrectionByDate.TryGetValue(workDate, out var correction);

                // Select one status by precedence so an existing shift or correction outranks a missing report.
                var status = shift is not null
                    ? shift.ClockOutAtUtc is null ? AttendanceDayStatus.Open : AttendanceDayStatus.Complete
                    : correction?.Status == AttendanceCorrectionStatus.PendingApproval ? AttendanceDayStatus.CorrectionPending
                    : correction?.Status == AttendanceCorrectionStatus.Rejected ? AttendanceDayStatus.CorrectionRejected
                    : workDate > todayLocalDate ? AttendanceDayStatus.Future
                    : !scheduleConfigured ? AttendanceDayStatus.ScheduleNotConfigured
                    : isWorkDay == false ? AttendanceDayStatus.NonWorking
                    : workDate < todayLocalDate ? AttendanceDayStatus.Missing
                    : AttendanceDayStatus.NoPunchToday;

                days.Add(new AttendanceCalendarDay(
                    workDate,
                    isWorkDay,
                    status,
                    shift?.ClockInTimeZoneId,
                    shift is null
                        ? null
                        : TimeZoneInfo.ConvertTime(
                            shift.ClockInAtUtc,
                            TimeZoneInfo.FindSystemTimeZoneById(shift.ClockInTimeZoneId)),
                    shift?.ClockOutTimeZoneId,
                    shift?.ClockOutAtUtc is { } clockOutAtUtc
                        ? TimeZoneInfo.ConvertTime(
                            clockOutAtUtc,
                            TimeZoneInfo.FindSystemTimeZoneById(shift.ClockOutTimeZoneId!))
                        : null,
                    correction?.Status.ToString()));
            }

            return new AttendanceCalendar(
                selectedYear,
                selectedMonth,
                timeZone.Id,
                todayLocalDate,
                currentPayrollPeriodStartDate,
                currentPayrollPeriodEndDate,
                reading.LocalTime,
                scheduleConfigured,
                days);
        }
        catch (DbException exception)
        {
            throw CreateUnavailableException(exception);
        }
    }

    public async Task<AttendanceState> GetCurrentAsync(
        AttendanceActor actor,
        CancellationToken cancellationToken)
    {
        // This endpoint reports shift state only; the calendar endpoint supplies the current company time.
        ArgumentNullException.ThrowIfNull(actor);

        try
        {
            // Confirm the membership is still active before reading a company-scoped shift.
            await EnsureActiveMembershipAsync(actor, cancellationToken);

            // Use a no-tracking lookup because this endpoint only reports whether an open shift exists.
            var openShift = await dbContext.WorkShifts
                .AsNoTracking()
                .SingleOrDefaultAsync(
                    shift => shift.CompanyId == actor.CompanyId &&
                             shift.EmployeeProfileId == actor.EmployeeProfileId &&
                             shift.ClockOutAtUtc == null,
                    cancellationToken);

            return openShift is null
                ? new AttendanceState(false, null)
                : new AttendanceState(true, ToOpenShiftDetails(openShift));
        }
        catch (DbException exception)
        {
            throw CreateUnavailableException(exception);
        }
    }

    public async Task<AttendancePunchResult> ClockInAsync(
        AttendanceActor actor,
        CancellationToken cancellationToken)
    {
        // Validate service-call input even though authenticated controllers normally always supply an actor.
        ArgumentNullException.ThrowIfNull(actor);

        try
        {
            // Recheck identity scope and read the company's current time zone before making attendance decisions.
            await EnsureActiveMembershipAsync(actor, cancellationToken);
            var timeZone = await GetCompanyTimeZoneAsync(actor.CompanyId, cancellationToken);

            // Reject a second open shift before requesting a provider sample or creating another row.
            var alreadyClockedIn = await dbContext.WorkShifts.AnyAsync(
                shift => shift.CompanyId == actor.CompanyId &&
                         shift.EmployeeProfileId == actor.EmployeeProfileId &&
                         shift.ClockOutAtUtc == null,
                cancellationToken);

            if (alreadyClockedIn)
            {
                throw new AttendanceConflictException(
                    "An open shift already exists for this employee.");
            }

            // Obtain the current time from the synchronized external clock or its immediate provider fallback.
            var reading = await GetTimeReadingAsync(timeZone.Id, cancellationToken);

            // Assign the shift to the company-local date produced by the authoritative time reading.
            var workDate = DateOnly.FromDateTime(reading.LocalTime.DateTime);

            // Resolve the work dates for this exact month before deciding if Clock In is permitted.
            var (_, configuredWorkDates) = await GetConfiguredWorkDatesAsync(
                actor.CompanyId,
                actor.EmployeeProfileId,
                workDate.Year,
                workDate.Month,
                cancellationToken);
            var isScheduledWorkDate = configuredWorkDates.Contains(workDate);

            if (!isScheduledWorkDate)
            {
                throw new AttendanceConflictException(
                    "Clock In is allowed only on a work date configured by payroll.");
            }

            // A pending correction owns this date until a reviewer decides it, so live punches are blocked.
            var correctionIsPending = await dbContext.AttendanceCorrectionRequests.AnyAsync(
                request => request.CompanyId == actor.CompanyId &&
                           request.EmployeeProfileId == actor.EmployeeProfileId &&
                           request.WorkDate == workDate &&
                           request.Status == AttendanceCorrectionStatus.PendingApproval,
                cancellationToken);
            if (correctionIsPending)
            {
                throw new AttendanceConflictException(
                    "A correction request is pending for this company-local work date.");
            }

            // Enforce the one-shift-per-workday rule before constructing a new shift.
            var alreadyClockedInOnWorkDate = await dbContext.WorkShifts.AnyAsync(
                shift => shift.CompanyId == actor.CompanyId &&
                         shift.EmployeeProfileId == actor.EmployeeProfileId &&
                         shift.WorkDate == workDate,
                cancellationToken);

            if (alreadyClockedInOnWorkDate)
            {
                throw new AttendanceConflictException(
                    "A shift has already been recorded for this company-local work date.");
            }

            // Create a domain entity that stores UTC, company zone, provider provenance, and local work date.
            var shift = WorkShift.ClockIn(
                actor.CompanyId,
                actor.EmployeeProfileId,
                reading.UtcTime,
                reading.Provider,
                reading.ProviderSampleUtcTime,
                timeZone.Id,
                workDate);

            // Stage the shift and persist it; database unique indexes protect against concurrent duplicate requests.
            dbContext.WorkShifts.Add(shift);
            try
            {
                await dbContext.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateException exception) when (IsOpenShiftUniqueConstraint(exception))
            {
                // Translate the database's open-shift uniqueness race into the same business conflict as the precheck.
                throw new AttendanceConflictException(
                    "An open shift already exists for this employee.");
            }
            catch (DbUpdateException exception)
            {
                // Convert other persistence failures to the service-unavailable boundary used by the API.
                throw CreateUnavailableException(exception);
            }

            // Return the saved punch and the provider sample used to create it.
            return CreatePunchResult(
                shift.WorkShiftId,
                "ClockedIn",
                shift.ClockInAtUtc,
                reading,
                timeZone);
        }
        catch (DbException exception)
        {
            // Normalize SQL connection/query failures for the API error mapper.
            throw CreateUnavailableException(exception);
        }
    }

    public async Task<AttendancePunchResult> ClockOutAsync(
        AttendanceActor actor,
        CancellationToken cancellationToken)
    {
        // Validate service-call input before looking up the employee's open shift.
        ArgumentNullException.ThrowIfNull(actor);

        try
        {
            // Recheck the active membership and load the zone to use for this company's punch.
            await EnsureActiveMembershipAsync(actor, cancellationToken);
            var timeZone = await GetCompanyTimeZoneAsync(actor.CompanyId, cancellationToken);

            // Require one existing open shift; Clock Out never creates a standalone attendance row.
            var openShift = await dbContext.WorkShifts.SingleOrDefaultAsync(
                shift => shift.CompanyId == actor.CompanyId &&
                         shift.EmployeeProfileId == actor.EmployeeProfileId &&
                         shift.ClockOutAtUtc == null,
                cancellationToken);

            if (openShift is null)
            {
                throw new AttendanceConflictException(
                    "There is no open shift to clock out from.");
            }

            // Read current time through the same synchronized external-clock and fallback path as Clock In.
            var reading = await GetTimeReadingAsync(timeZone.Id, cancellationToken);

            // Keep the Clock In work date for overnight shifts and correction checks on the following day.
            var shiftWorkDate = openShift.WorkDate;

            // A correction request pending for this shift prevents a conflicting live Clock Out.
            var correctionIsPending = await dbContext.AttendanceCorrectionRequests.AnyAsync(
                request => request.CompanyId == actor.CompanyId &&
                           request.EmployeeProfileId == actor.EmployeeProfileId &&
                           request.WorkDate == shiftWorkDate &&
                           request.Status == AttendanceCorrectionStatus.PendingApproval,
                cancellationToken);
            if (correctionIsPending)
            {
                throw new AttendanceConflictException(
                    "A correction request is pending for this shift.");
            }

            // Guard against a provider sample that would place the exit before the recorded entry.
            if (reading.UtcTime < openShift.ClockInAtUtc)
            {
                throw new AttendanceUnavailableException(
                    "The current company-local time is earlier than the open shift's clock-in time.");
            }

            // Enforce the fixed 24-hour maximum; longer shifts must use the manager correction workflow.
            if (!WorkShift.IsDurationWithinMaximum(openShift.ClockInAtUtc, reading.UtcTime))
            {
                throw new AttendanceConflictException(
                    $"Clock Out must be recorded within {WorkShift.MaximumDurationHours} hours of Clock In. Submit a correction request for manager review.");
            }

            // Attach the external sample and the zone used for this specific exit to the existing entity.
            openShift.ClockOut(
                reading.UtcTime,
                reading.Provider,
                reading.ProviderSampleUtcTime,
                timeZone.Id);

            try
            {
                await dbContext.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateConcurrencyException)
            {
                // RowVersion detected that another request or admin changed this shift after it was read.
                throw new AttendanceConflictException(
                    "The open shift changed while this request was being processed.");
            }
            catch (DbUpdateException exception)
            {
                // Keep database write failures behind the same unavailable-service response.
                throw CreateUnavailableException(exception);
            }

            // Return the closed shift with the exact server-computed local display time.
            return CreatePunchResult(
                openShift.WorkShiftId,
                "ClockedOut",
                openShift.ClockOutAtUtc!.Value,
                reading,
                timeZone);
        }
        catch (DbException exception)
        {
            // Normalize SQL connection/query failures for the API error mapper.
            throw CreateUnavailableException(exception);
        }
    }

    // Revalidate ticket identity against the current company, user, membership, and employee-profile rows.
    private async Task EnsureActiveMembershipAsync(
        AttendanceActor actor,
        CancellationToken cancellationToken)
    {
        // A no-tracking existence query avoids loading the full membership graph for each attendance operation.
        var isActive = await dbContext.CompanyMemberships
            .AsNoTracking()
            .AnyAsync(
                membership => membership.MembershipId == actor.MembershipId &&
                              membership.CompanyId == actor.CompanyId &&
                              membership.UserId == actor.UserId &&
                              membership.IsActive &&
                              membership.Company.IsActive &&
                              membership.EmployeeProfile != null &&
                              membership.EmployeeProfile.EmployeeProfileId == actor.EmployeeProfileId,
                cancellationToken);

        if (!isActive)
        {
            throw new AttendanceAccessDeniedException(
                "The authenticated company membership is no longer active.");
        }
    }

    // Prefer an employee's saved monthly dates; use company-default weekdays only when no monthly schedule exists.
    private async Task<(bool IsConfigured, HashSet<DateOnly> WorkDates)> GetConfiguredWorkDatesAsync(
        Guid companyId,
        Guid employeeProfileId,
        int year,
        int month,
        CancellationToken cancellationToken)
    {
        // Load the monthly schedule and its date rows together so this month can override company defaults.
        var employeeSchedule = await dbContext.EmployeeMonthlyWorkSchedules
            .AsNoTracking()
            .Include(schedule => schedule.WorkingDays)
            .Where(schedule => schedule.CompanyId == companyId &&
                               schedule.EmployeeProfileId == employeeProfileId &&
                               schedule.Year == year &&
                               schedule.Month == month)
            .SingleOrDefaultAsync(cancellationToken);

        if (employeeSchedule is not null)
        {
            // An explicitly saved schedule is authoritative, including an intentionally empty list.
            return (true, employeeSchedule.WorkingDays.Select(day => day.WorkDate).ToHashSet());
        }

        // Without a monthly override, expand the company's recurring weekdays into concrete dates for this month.
        var companyDefaultDays = await dbContext.CompanyDefaultWorkDays
            .AsNoTracking()
            .Where(day => day.CompanyId == companyId)
            .Select(day => day.DayOfWeek)
            .ToArrayAsync(cancellationToken);
        if (companyDefaultDays.Length == 0)
        {
            // An empty defaults list means Payroll has not configured a usable schedule yet.
            return (false, []);
        }

        // Convert recurring day-of-week values into the date set used by Clock In validation.
        var defaultDaySet = companyDefaultDays.ToHashSet();
        var daysInMonth = DateTime.DaysInMonth(year, month);
        var workDates = Enumerable.Range(1, daysInMonth)
            .Select(dayNumber => new DateOnly(year, month, dayNumber))
            .Where(date => defaultDaySet.Contains(date.DayOfWeek))
            .ToHashSet();

        return (true, workDates);
    }

    // Use the attendance-specific clock path, which tries providers immediately if its cached sample is stale.
    private async Task<ClockReading> GetTimeReadingAsync(string timeZoneId, CancellationToken cancellationToken)
    {
        try
        {
            return await attendanceClock.GetCurrentTimeForAttendanceAsync(timeZoneId, cancellationToken);
        }
        catch (ClockUnavailableException exception)
        {
            // Translate clock-specific failure into the attendance layer's service-unavailable exception.
            throw new AttendanceUnavailableException(
                "No valid external time reading is available for this company.",
                exception);
        }
    }

    // Read the company's current payroll zone, register it for background refresh, or use the Israel default.
    private async Task<TimeZoneInfo> GetCompanyTimeZoneAsync(Guid companyId, CancellationToken cancellationToken)
    {
        var configuredTimeZoneId = await dbContext.CompanyPayrollSettings
            .AsNoTracking()
            .Where(settings => settings.CompanyId == companyId)
            .Select(settings => settings.TimeZoneId)
            .SingleOrDefaultAsync(cancellationToken);
        // Resolve and register the selected zone before asking the synchronized clock for a sample.
        var timeZoneId = configuredTimeZoneId ?? timeZoneRegistry.DefaultTimeZoneId;
        return TimeZoneInfo.FindSystemTimeZoneById(timeZoneRegistry.Register(timeZoneId));
    }

    // Project an open shift into API data while preserving the zone captured at Clock In.
    private static OpenShiftDetails ToOpenShiftDetails(WorkShift shift) => new(
        shift.WorkShiftId,
        shift.ClockInAtUtc,
        shift.ClockInTimeZoneId,
        TimeZoneInfo.ConvertTime(
            shift.ClockInAtUtc,
            TimeZoneInfo.FindSystemTimeZoneById(shift.ClockInTimeZoneId)),
        shift.ClockInProvider,
        shift.ClockInProviderSampleUtc);

    // Return UTC for stable storage/client logic and local time plus provenance for user display and audit.
    private static AttendancePunchResult CreatePunchResult(
        Guid workShiftId,
        string status,
        DateTimeOffset occurredAtUtc,
        ClockReading reading,
        TimeZoneInfo timeZone) => new(
        workShiftId,
        status,
        occurredAtUtc,
        TimeZoneInfo.ConvertTime(occurredAtUtc, timeZone),
        reading.TimeZone,
        reading.Provider,
        reading.ProviderSampleUtcTime);

    private static void ValidateCalendarMonth(int year, int month)
    {
        if (year is < 2000 or > 9999) throw new ArgumentOutOfRangeException(nameof(year));
        if (month is < 1 or > 12) throw new ArgumentOutOfRangeException(nameof(month));
    }

    private static (DateOnly StartDate, DateOnly EndDate) GetCurrentPayrollPeriod(
        DateOnly today,
        int payrollPeriodStartDay)
    {
        var periodStartThisMonth = GetPayrollPeriodStart(today.Year, today.Month, payrollPeriodStartDay);
        var periodStart = today >= periodStartThisMonth
            ? periodStartThisMonth
            : GetPayrollPeriodStart(today.AddMonths(-1).Year, today.AddMonths(-1).Month, payrollPeriodStartDay);
        var nextPeriodStartMonth = periodStart.AddMonths(1);
        var nextPeriodStart = GetPayrollPeriodStart(
            nextPeriodStartMonth.Year,
            nextPeriodStartMonth.Month,
            payrollPeriodStartDay);

        return (periodStart, nextPeriodStart.AddDays(-1));
    }

    private static DateOnly GetPayrollPeriodStart(int year, int month, int payrollPeriodStartDay) =>
        new(year, month, Math.Min(payrollPeriodStartDay, DateTime.DaysInMonth(year, month)));

    // Identify SQL Server's duplicate-key errors for the filtered unique index on open shifts.
    private static bool IsOpenShiftUniqueConstraint(DbUpdateException exception) =>
        exception.InnerException is SqlException { Number: 2601 or 2627 } sqlException &&
        sqlException.Message.Contains(OpenShiftIndexName, StringComparison.OrdinalIgnoreCase);

    // Keep implementation-specific database details out of the attendance API's public error contract.
    private static AttendanceUnavailableException CreateUnavailableException(Exception exception) =>
        new("Attendance data could not be read or saved.", exception);
}
