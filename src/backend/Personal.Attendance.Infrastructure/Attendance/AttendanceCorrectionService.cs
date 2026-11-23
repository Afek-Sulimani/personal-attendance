using System.Data;
using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Personal.Attendance.Application.Attendance;
using Personal.Attendance.Application.Time;
using Personal.Attendance.Domain.Entities;
using Personal.Attendance.Domain.Enums;
using Personal.Attendance.Infrastructure.Persistence;

namespace Personal.Attendance.Infrastructure.Attendance;

public sealed class AttendanceCorrectionService(
    PersonalAttendanceDbContext dbContext,
    ICurrentAttendanceClock attendanceClock,
    TimeProvider timeProvider,
    ICompanyTimeZoneRegistry timeZoneRegistry) : IAttendanceCorrectionService
{
    private const string PendingPerWorkDateIndexName =
        "IX_ATTENDANCE_ATTENDANCE_CORRECTION_REQUESTS_CompanyId_EmployeeProfileId_WorkDate";
    private const string OpenShiftIndexName =
        "IX_ATTENDANCE_WORK_SHIFTS_CompanyId_EmployeeProfileId";

    public async Task<Guid> SubmitAsync(
        SubmitAttendanceCorrectionCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ValidateCommand(command);

        try
        {
            var requester = await GetActiveMembershipAsync(command.Actor, requireProfile: true, cancellationToken);
            var timeZone = await GetCompanyTimeZoneAsync(command.Actor.CompanyId, cancellationToken);
            var reading = await GetTimeReadingAsync(timeZone.Id, cancellationToken);
            var todayLocalDate = DateOnly.FromDateTime(reading.LocalTime.DateTime);

            if (command.WorkDate > todayLocalDate)
            {
                throw new AttendanceCorrectionConflictException("A correction cannot be requested for a future work date.");
            }

            var isConfiguredWorkDate = await dbContext.EmployeeMonthlyWorkDays
                .AsNoTracking()
                .AnyAsync(day => day.CompanyId == command.Actor.CompanyId &&
                                day.WorkDate == command.WorkDate &&
                                day.Schedule.EmployeeProfileId == command.Actor.EmployeeProfileId,
                    cancellationToken);
            var hasPersonalSchedule = await dbContext.EmployeeMonthlyWorkSchedules.AsNoTracking()
                .AnyAsync(schedule => schedule.CompanyId == command.Actor.CompanyId &&
                    schedule.EmployeeProfileId == command.Actor.EmployeeProfileId &&
                    schedule.Year == command.WorkDate.Year && schedule.Month == command.WorkDate.Month,
                    cancellationToken);
            if (!isConfiguredWorkDate && !hasPersonalSchedule)
            {
                isConfiguredWorkDate = await dbContext.CompanyDefaultWorkDays
                    .AsNoTracking()
                    .AnyAsync(day => day.CompanyId == command.Actor.CompanyId &&
                                     day.DayOfWeek == command.WorkDate.DayOfWeek,
                        cancellationToken);
            }
            if (!isConfiguredWorkDate)
            {
                throw new AttendanceCorrectionConflictException("Payroll has not configured this date as a workday.");
            }

            var shift = await dbContext.WorkShifts.SingleOrDefaultAsync(
                item => item.CompanyId == command.Actor.CompanyId &&
                        item.EmployeeProfileId == command.Actor.EmployeeProfileId &&
                        item.WorkDate == command.WorkDate,
                cancellationToken);
            var correctionTimeZone = shift is { IsOpen: true }
                ? TimeZoneInfo.FindSystemTimeZoneById(
                    timeZoneRegistry.Register(shift.ClockInTimeZoneId))
                : timeZone;

            DateTimeOffset? requestedClockInAtUtc;
            DateTimeOffset? requestedClockOutAtUtc;
            Guid? shiftId;

            if (shift is { IsOpen: true })
            {
                if (command.WorkDate >= todayLocalDate ||
                    command.ClockInLocalTime is not null ||
                    command.ClockOutLocalTime is null)
                {
                    throw new AttendanceCorrectionConflictException(
                        "A past open shift correction must contain only the corrected clock-out time.");
                }

                requestedClockInAtUtc = null;
                requestedClockOutAtUtc = ConvertSelectedTimeToUtc(
                    command.WorkDate,
                    command.ClockOutLocalTime.Value,
                    command.ClockOutNextDay,
                    correctionTimeZone);
                shiftId = shift.WorkShiftId;
                if (requestedClockOutAtUtc < shift.ClockInAtUtc)
                {
                    throw new AttendanceCorrectionConflictException("Clock Out cannot be earlier than the recorded Clock In.");
                }
                if (!WorkShift.IsDurationWithinMaximum(shift.ClockInAtUtc, requestedClockOutAtUtc.Value))
                {
                    throw new AttendanceCorrectionConflictException(
                        $"A shift cannot exceed {WorkShift.MaximumDurationHours} hours from Clock In to Clock Out.");
                }
            }
            else
            {
                if (shift is not null)
                {
                    throw new AttendanceCorrectionConflictException(
                        "This date already has a completed shift and cannot receive another correction request.");
                }

                if (command.ClockInLocalTime is null)
                {
                    throw new AttendanceCorrectionConflictException("A missing shift correction must include a clock-in time.");
                }

                if (command.WorkDate == todayLocalDate && command.ClockOutLocalTime is not null)
                {
                    throw new AttendanceCorrectionConflictException(
                        "For today, submit a clock-in correction only; clock out using the live clock when the shift ends.");
                }

                if (command.WorkDate < todayLocalDate && command.ClockOutLocalTime is null)
                {
                    throw new AttendanceCorrectionConflictException(
                        "A missing shift from a past workday must include both clock-in and clock-out times.");
                }

                requestedClockInAtUtc = ConvertSelectedTimeToUtc(
                    command.WorkDate,
                    command.ClockInLocalTime.Value,
                    clockOutNextDay: false,
                    correctionTimeZone);
                requestedClockOutAtUtc = command.ClockOutLocalTime is { } clockOutTime
                    ? ConvertSelectedTimeToUtc(command.WorkDate, clockOutTime, command.ClockOutNextDay, correctionTimeZone)
                    : null;
                shiftId = null;

                if (requestedClockOutAtUtc is { } requestedClockOut &&
                    !WorkShift.IsDurationWithinMaximum(requestedClockInAtUtc.Value, requestedClockOut))
                {
                    throw new AttendanceCorrectionConflictException(
                        $"A shift cannot exceed {WorkShift.MaximumDurationHours} hours from Clock In to Clock Out.");
                }

                if (requestedClockInAtUtc > reading.UtcTime || requestedClockOutAtUtc > reading.UtcTime)
                {
                    throw new AttendanceCorrectionConflictException("A correction cannot contain a time later than the current company-local time.");
                }
            }

            if (requestedClockOutAtUtc > reading.UtcTime)
            {
                throw new AttendanceCorrectionConflictException("A correction cannot contain a time later than the current company-local time.");
            }

            var directApproverMembershipId = await ResolveDirectApproverAsync(
                requester,
                command.Actor,
                cancellationToken);

            var request = AttendanceCorrectionRequest.Create(
                command.Actor.CompanyId,
                command.Actor.EmployeeProfileId,
                command.Actor.MembershipId,
                shiftId,
                directApproverMembershipId,
                command.WorkDate,
                correctionTimeZone.Id,
                requestedClockInAtUtc,
                requestedClockOutAtUtc,
                reading.UtcTime,
                reading.Provider,
                reading.ProviderSampleUtcTime);

            try
            {
                if (dbContext.Database.IsRelational())
                {
                    await using var transaction = await dbContext.Database.BeginTransactionAsync(
                        IsolationLevel.Serializable, cancellationToken);
                    await SaveReplacingPendingRequestAsync(request, command.Actor, cancellationToken);
                    await transaction.CommitAsync(cancellationToken);
                }
                else
                {
                    await SaveReplacingPendingRequestAsync(request, command.Actor, cancellationToken);
                }
            }
            catch (DbUpdateException exception) when (IsPendingDateConstraint(exception))
            {
                throw new AttendanceCorrectionConflictException(
                    "Another correction request was submitted at the same time. Refresh the attendance page and try again.");
            }
            catch (DbUpdateException exception)
            {
                throw CreateUnavailableException(exception);
            }

            return request.RequestId;
        }
        catch (DbException exception)
        {
            throw CreateUnavailableException(exception);
        }
    }

    private async Task SaveReplacingPendingRequestAsync(
        AttendanceCorrectionRequest replacement,
        AttendanceActor actor,
        CancellationToken cancellationToken)
    {
        var pending = await dbContext.AttendanceCorrectionRequests.SingleOrDefaultAsync(
            request => request.CompanyId == actor.CompanyId &&
                       request.EmployeeProfileId == actor.EmployeeProfileId &&
                       request.WorkDate == replacement.WorkDate &&
                       request.Status == AttendanceCorrectionStatus.PendingApproval,
            cancellationToken);

        if (pending is not null)
        {
            pending.Supersede();
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        dbContext.AttendanceCorrectionRequests.Add(replacement);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<AttendanceCorrectionSummary>> GetPendingAsync(
        AttendanceActor actor,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(actor);

        try
        {
            var reviewer = await GetActiveMembershipAsync(actor, requireProfile: false, cancellationToken);
            var canReviewCompanyFallback = reviewer.Roles.Any(role =>
                role.Role is CompanyRole.Hr or CompanyRole.PayrollRepresentative);
            var isDirectManagerRole = reviewer.Roles.Any(role => role.Role is CompanyRole.TeamLead or CompanyRole.Admin);
            if (!canReviewCompanyFallback && !isDirectManagerRole)
            {
                throw new AttendanceCorrectionAccessDeniedException("A direct manager, HR, or Payroll role is required to review corrections.");
            }

            var requests = await (
                from request in dbContext.AttendanceCorrectionRequests.AsNoTracking()
                join profile in dbContext.EmployeeProfiles.AsNoTracking()
                    on new { request.CompanyId, request.EmployeeProfileId }
                    equals new { profile.CompanyId, profile.EmployeeProfileId }
                where request.CompanyId == actor.CompanyId &&
                      request.Status == AttendanceCorrectionStatus.PendingApproval &&
                      (request.DirectApproverMembershipId == actor.MembershipId ||
                       (request.DirectApproverMembershipId == null &&
                        canReviewCompanyFallback &&
                        request.RequestedByMembershipId != actor.MembershipId &&
                        !dbContext.CompanyMemberships.Any(membership =>
                            membership.CompanyId == request.CompanyId &&
                            membership.MembershipId == request.RequestedByMembershipId &&
                            membership.ManagerMembershipId != null)))
                orderby request.SubmittedAtUtc
                select new
                {
                    request.RequestId,
                    profile.DisplayName,
                    profile.EmployeeNumber,
                    request.WorkDate,
                    request.TimeZoneId,
                    request.RequestedClockInAtUtc,
                    request.RequestedClockOutAtUtc,
                    request.SubmittedAtUtc,
                    request.Status,
                    request.RejectionReason
                }).ToListAsync(cancellationToken);

            var settings = await dbContext.CompanyPayrollSettings.AsNoTracking()
                .SingleOrDefaultAsync(item => item.CompanyId == actor.CompanyId, cancellationToken);
            var companyZone = settings is null ? null : TimeZoneInfo.FindSystemTimeZoneById(
                timeZoneRegistry.Register(settings.TimeZoneId));
            var nowUtc = timeProvider.GetUtcNow();

            return requests.Select(request =>
            {
                var requestTimeZone = TimeZoneInfo.FindSystemTimeZoneById(
                    timeZoneRegistry.Register(request.TimeZoneId));
                return new AttendanceCorrectionSummary(
                    request.RequestId,
                    request.DisplayName,
                    request.EmployeeNumber,
                    request.WorkDate,
                    request.TimeZoneId,
                    ToLocalTime(request.RequestedClockInAtUtc, requestTimeZone),
                    ToLocalTime(request.RequestedClockOutAtUtc, requestTimeZone),
                    ToLocalTime(request.SubmittedAtUtc, requestTimeZone)!.Value,
                    request.Status.ToString(),
                    request.RejectionReason,
                    settings is not null && companyZone is not null &&
                    CorrectionAttentionPolicy.IsAttentionRequired(request.WorkDate, nowUtc,
                        settings.PayrollPeriodStartDay, companyZone));
            }).ToArray();
        }
        catch (DbException exception)
        {
            throw CreateUnavailableException(exception);
        }
    }

    public async Task ApproveAsync(
        AttendanceActor actor,
        Guid requestId,
        CancellationToken cancellationToken)
    {
        await ReviewAsync(actor, requestId, null, cancellationToken);
    }

    public async Task RejectAsync(
        AttendanceActor actor,
        Guid requestId,
        string reason,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new ArgumentException("A rejection reason is required.", nameof(reason));
        }

        await ReviewAsync(actor, requestId, reason, cancellationToken);
    }

    private async Task ReviewAsync(
        AttendanceActor actor,
        Guid requestId,
        string? rejectionReason,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(actor);
        if (requestId == Guid.Empty) throw new ArgumentException("A correction request ID is required.", nameof(requestId));

        try
        {
            await using var transaction = await dbContext.Database.BeginTransactionAsync(
                IsolationLevel.Serializable,
                cancellationToken);

            var reviewer = await GetActiveMembershipAsync(actor, requireProfile: false, cancellationToken);
            var request = await dbContext.AttendanceCorrectionRequests.SingleOrDefaultAsync(
                item => item.CompanyId == actor.CompanyId && item.RequestId == requestId,
                cancellationToken);

            if (request is null)
            {
                throw new AttendanceCorrectionNotFoundException("The correction request was not found.");
            }
            if (request.Status != AttendanceCorrectionStatus.PendingApproval)
            {
                throw new AttendanceCorrectionConflictException("This correction request has already been reviewed.");
            }
            if (!await CanReviewAsync(request, reviewer, cancellationToken))
            {
                throw new AttendanceCorrectionAccessDeniedException("You are not an authorized approver for this correction.");
            }

            var reviewedAtUtc = timeProvider.GetUtcNow().ToUniversalTime();
            if (rejectionReason is not null)
            {
                request.Reject(actor.MembershipId, reviewedAtUtc, rejectionReason);
            }
            else
            {
                request.Approve(actor.MembershipId, reviewedAtUtc);
                await ApplyApprovedCorrectionAsync(request, cancellationToken);
            }

            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new AttendanceCorrectionConflictException("The correction request changed while it was being reviewed.");
        }
        catch (DbUpdateException exception) when (IsOpenShiftUniqueConstraint(exception))
        {
            throw new AttendanceCorrectionConflictException(
                "An open shift was created before this correction could be approved.");
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

    private async Task ApplyApprovedCorrectionAsync(
        AttendanceCorrectionRequest request,
        CancellationToken cancellationToken)
    {
        if (request.WorkShiftId is { } existingShiftId)
        {
            var shift = await dbContext.WorkShifts.SingleOrDefaultAsync(
                item => item.CompanyId == request.CompanyId &&
                        item.WorkShiftId == existingShiftId &&
                        item.EmployeeProfileId == request.EmployeeProfileId,
                cancellationToken);
            if (shift is null || !shift.IsOpen || request.RequestedClockOutAtUtc is null)
            {
                throw new AttendanceCorrectionConflictException("The open shift changed before this correction was approved.");
            }

            if (!WorkShift.IsDurationWithinMaximum(shift.ClockInAtUtc, request.RequestedClockOutAtUtc.Value))
            {
                throw new AttendanceCorrectionConflictException(
                    $"A shift cannot exceed {WorkShift.MaximumDurationHours} hours from Clock In to Clock Out.");
            }

            shift.SetClockOutFromApprovedCorrection(
                request.RequestedClockOutAtUtc.Value,
                request.TimeZoneId);
            return;
        }

        if (request.RequestedClockInAtUtc is null)
        {
            throw new AttendanceCorrectionConflictException("The correction does not contain a clock-in time.");
        }

        var shiftAlreadyExists = await dbContext.WorkShifts.AnyAsync(
            item => item.CompanyId == request.CompanyId &&
                    item.EmployeeProfileId == request.EmployeeProfileId &&
                    item.WorkDate == request.WorkDate,
            cancellationToken);
        if (shiftAlreadyExists)
        {
            throw new AttendanceCorrectionConflictException("A shift already exists for this work date.");
        }

        if (request.RequestedClockOutAtUtc is { } requestedClockOutAtUtc &&
            !WorkShift.IsDurationWithinMaximum(request.RequestedClockInAtUtc.Value, requestedClockOutAtUtc))
        {
            throw new AttendanceCorrectionConflictException(
                $"A shift cannot exceed {WorkShift.MaximumDurationHours} hours from Clock In to Clock Out.");
        }

        var correctedShift = WorkShift.CreateFromApprovedCorrection(
            request.CompanyId,
            request.EmployeeProfileId,
            request.WorkDate,
            request.RequestedClockInAtUtc.Value,
            request.RequestedClockOutAtUtc,
            request.TimeZoneId);
        request.SetApprovedWorkShiftId(correctedShift.WorkShiftId);
        dbContext.WorkShifts.Add(correctedShift);
    }

    private async Task<bool> CanReviewAsync(
        AttendanceCorrectionRequest request,
        CompanyMembership reviewer,
        CancellationToken cancellationToken)
    {
        if (request.RequestedByMembershipId == reviewer.MembershipId) return false;
        if (request.DirectApproverMembershipId == reviewer.MembershipId) return true;
        if (request.DirectApproverMembershipId is not null ||
            !reviewer.Roles.Any(role => role.Role is CompanyRole.Hr or CompanyRole.PayrollRepresentative))
        {
            return false;
        }

        return !await dbContext.CompanyMemberships
            .AsNoTracking()
            .AnyAsync(membership => membership.MembershipId == request.RequestedByMembershipId &&
                                    membership.CompanyId == request.CompanyId &&
                                    membership.ManagerMembershipId != null,
                cancellationToken);
    }

    private async Task<Guid?> ResolveDirectApproverAsync(
        CompanyMembership requester,
        AttendanceActor actor,
        CancellationToken cancellationToken)
    {
        if (requester.ManagerMembershipId is { } managerMembershipId)
        {
            if (managerMembershipId == requester.MembershipId)
            {
                throw new AttendanceCorrectionConflictException("A user cannot approve their own correction.");
            }

            var managerIsActive = await dbContext.CompanyMemberships
                .AsNoTracking()
                .AnyAsync(membership => membership.MembershipId == managerMembershipId &&
                                        membership.CompanyId == actor.CompanyId &&
                                        membership.IsActive &&
                                        membership.Company.IsActive,
                    cancellationToken);
            if (!managerIsActive)
            {
                throw new AttendanceCorrectionConflictException("The assigned manager is not active in this company.");
            }

            return managerMembershipId;
        }

        var canUseCompanyFallback = requester.Roles.Any(role =>
            role.Role is CompanyRole.Hr or CompanyRole.PayrollRepresentative);
        if (!canUseCompanyFallback)
        {
            throw new AttendanceCorrectionConflictException("An assigned manager is required for this correction.");
        }

        var hasAlternateReviewer = await dbContext.CompanyMemberships
            .AsNoTracking()
            .AnyAsync(membership => membership.CompanyId == actor.CompanyId &&
                                    membership.MembershipId != requester.MembershipId &&
                                    membership.IsActive &&
                                    membership.Company.IsActive &&
                                    membership.Roles.Any(role =>
                                        role.Role == CompanyRole.Hr ||
                                        role.Role == CompanyRole.PayrollRepresentative),
                cancellationToken);
        if (!hasAlternateReviewer)
        {
            throw new AttendanceCorrectionConflictException(
                "A different active HR or Payroll representative must be available to approve this request.");
        }

        return null;
    }

    private async Task<CompanyMembership> GetActiveMembershipAsync(
        AttendanceActor actor,
        bool requireProfile,
        CancellationToken cancellationToken)
    {
        if (actor.UserId == Guid.Empty || actor.CompanyId == Guid.Empty || actor.MembershipId == Guid.Empty ||
            (requireProfile && actor.EmployeeProfileId == Guid.Empty))
        {
            throw new AttendanceCorrectionAccessDeniedException("The session does not identify an active company member.");
        }

        var membership = await dbContext.CompanyMemberships
            .Include(item => item.Roles)
            .SingleOrDefaultAsync(item => item.MembershipId == actor.MembershipId &&
                                          item.CompanyId == actor.CompanyId &&
                                          item.UserId == actor.UserId &&
                                          item.IsActive && item.Company.IsActive,
                cancellationToken);
        if (membership is null)
        {
            throw new AttendanceCorrectionAccessDeniedException("The authenticated company membership is no longer active.");
        }

        if (requireProfile && !await dbContext.EmployeeProfiles.AnyAsync(
                profile => profile.CompanyId == actor.CompanyId &&
                           profile.EmployeeProfileId == actor.EmployeeProfileId &&
                           profile.MembershipId == actor.MembershipId,
                cancellationToken))
        {
            throw new AttendanceCorrectionAccessDeniedException("The session does not identify an employee profile in this company.");
        }

        return membership;
    }

    private async Task<ClockReading> GetTimeReadingAsync(string timeZoneId, CancellationToken cancellationToken)
    {
        try
        {
            return await attendanceClock.GetCurrentTimeForAttendanceAsync(timeZoneId, cancellationToken);
        }
        catch (ClockUnavailableException exception)
        {
            throw new AttendanceCorrectionUnavailableException("No valid external company time reading is available.", exception);
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

    private static DateTimeOffset ConvertSelectedTimeToUtc(
        DateOnly workDate,
        TimeOnly time,
        bool clockOutNextDay,
        TimeZoneInfo timeZone)
    {
        var localDate = clockOutNextDay ? workDate.AddDays(1) : workDate;
        var localDateTime = DateTime.SpecifyKind(localDate.ToDateTime(time), DateTimeKind.Unspecified);
        if (timeZone.IsInvalidTime(localDateTime))
        {
            throw new AttendanceCorrectionConflictException("The selected local time does not exist because of a daylight-saving change.");
        }
        if (timeZone.IsAmbiguousTime(localDateTime))
        {
            throw new AttendanceCorrectionConflictException("The selected local time occurs twice because of a daylight-saving change.");
        }

        return new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(localDateTime, timeZone));
    }

    private static DateTimeOffset? ToLocalTime(DateTimeOffset? value, TimeZoneInfo timeZone) =>
        value is null ? null : TimeZoneInfo.ConvertTime(value.Value, timeZone);

    private static void ValidateCommand(SubmitAttendanceCorrectionCommand command)
    {
        if (command.ClockInLocalTime is null && command.ClockOutLocalTime is null)
        {
            throw new ArgumentException("At least one corrected time is required.");
        }
        if (command.ClockOutNextDay && command.ClockOutLocalTime is null)
        {
            throw new ArgumentException("Clock Out next day requires a clock-out time.");
        }
    }

    private static bool IsPendingDateConstraint(DbUpdateException exception) =>
        exception.InnerException is Microsoft.Data.SqlClient.SqlException { Number: 2601 or 2627 } sqlException &&
        sqlException.Message.Contains(PendingPerWorkDateIndexName, StringComparison.OrdinalIgnoreCase);

    private static bool IsOpenShiftUniqueConstraint(DbUpdateException exception) =>
        exception.InnerException is Microsoft.Data.SqlClient.SqlException { Number: 2601 or 2627 } sqlException &&
        sqlException.Message.Contains(OpenShiftIndexName, StringComparison.OrdinalIgnoreCase);

    private static AttendanceCorrectionUnavailableException CreateUnavailableException(Exception exception) =>
        new("Attendance correction data could not be read or saved.", exception);
}
