using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Personal.Attendance.Api.Authentication;
using Personal.Attendance.Application.Attendance;

namespace Personal.Attendance.Api.Controllers;

[ApiController]
[Route("api/monthly-attendance-reports")]
[Authorize(AuthenticationSchemes = AttendanceAuthenticationDefaults.SchemeName)]
public sealed class MonthlyAttendanceReportsController(
    IMonthlyAttendanceReportService reportService,
    ILogger<MonthlyAttendanceReportsController> logger) : ControllerBase
{
    [HttpGet("mine/current")]
    [ProducesResponseType<MonthlyAttendanceReportResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult<MonthlyAttendanceReportResponse>> GetCurrentMine(CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actor)) return Unauthorized();
        return await ExecuteAsync(() => reportService.GetCurrentMineAsync(actor, cancellationToken));
    }

    [HttpGet("mine")]
    [ProducesResponseType<MonthlyAttendanceReportResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult<MonthlyAttendanceReportResponse>> GetMine(
        [FromQuery, Range(2000, 9998)] int year,
        [FromQuery, Range(1, 12)] int month,
        CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actor)) return Unauthorized();
        return await ExecuteAsync(() => reportService.GetMineAsync(actor, year, month, cancellationToken));
    }

    [HttpGet("team/members")]
    [ProducesResponseType<IReadOnlyList<MonthlyAttendanceTeamMemberResponse>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<MonthlyAttendanceTeamMemberResponse>>> GetTeamMembers(
        CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actor)) return Unauthorized();
        try
        {
            var members = await reportService.GetTeamMembersAsync(actor, cancellationToken);
            return Ok(members.Select(member => new MonthlyAttendanceTeamMemberResponse(
                member.EmployeeMembershipId,
                member.EmployeeName,
                member.EmployeeNumber)).ToArray());
        }
        catch (MonthlyAttendanceReportAccessDeniedException exception) { return ForbidResponse(exception.Message); }
        catch (MonthlyAttendanceReportUnavailableException) { return Unavailable(); }
    }

    [HttpGet("team/members/{employeeMembershipId:guid}/current")]
    [ProducesResponseType<MonthlyAttendanceReportResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult<MonthlyAttendanceReportResponse>> GetCurrentTeamMember(
        Guid employeeMembershipId,
        CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actor)) return Unauthorized();
        return await ExecuteAsync(() => reportService.GetCurrentTeamMemberAsync(actor, employeeMembershipId, cancellationToken));
    }

    [HttpGet("team/members/{employeeMembershipId:guid}")]
    [ProducesResponseType<MonthlyAttendanceReportResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult<MonthlyAttendanceReportResponse>> GetTeamMember(
        Guid employeeMembershipId,
        [FromQuery, Range(2000, 9998)] int year,
        [FromQuery, Range(1, 12)] int month,
        CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actor)) return Unauthorized();
        return await ExecuteAsync(() => reportService.GetTeamMemberAsync(
            actor, employeeMembershipId, year, month, cancellationToken));
    }

    private async Task<ActionResult<MonthlyAttendanceReportResponse>> ExecuteAsync(
        Func<Task<MonthlyAttendanceReportView>> operation)
    {
        try { return Ok(ToResponse(await operation())); }
        catch (MonthlyAttendanceReportNotFoundException exception) { return NotFound(new MonthlyAttendanceReportErrorResponse(exception.Message)); }
        catch (MonthlyAttendanceReportAccessDeniedException exception) { return ForbidResponse(exception.Message); }
        catch (MonthlyAttendanceReportConflictException exception) { return Conflict(new MonthlyAttendanceReportErrorResponse(exception.Message)); }
        catch (MonthlyAttendanceReportUnavailableException) { return Unavailable(); }
    }

    private ObjectResult ForbidResponse(string message) =>
        StatusCode(StatusCodes.Status403Forbidden, new MonthlyAttendanceReportErrorResponse(message));

    private ObjectResult Unavailable()
    {
        logger.LogWarning("A monthly attendance view operation could not be completed.");
        return StatusCode(StatusCodes.Status503ServiceUnavailable,
            new MonthlyAttendanceReportErrorResponse("Monthly attendance is temporarily unavailable."));
    }

    private bool TryGetActor(out AttendanceActor actor)
    {
        if (!Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId) ||
            !Guid.TryParse(User.FindFirstValue(AttendanceAuthenticationDefaults.CompanyIdClaim), out var companyId) ||
            !Guid.TryParse(User.FindFirstValue(AttendanceAuthenticationDefaults.MembershipIdClaim), out var membershipId) ||
            !Guid.TryParse(User.FindFirstValue(AttendanceAuthenticationDefaults.EmployeeProfileIdClaim), out var employeeProfileId))
        {
            actor = null!;
            return false;
        }

        actor = new AttendanceActor(userId, companyId, membershipId, employeeProfileId);
        return true;
    }

    private static MonthlyAttendanceReportResponse ToResponse(MonthlyAttendanceReportView view) => new(
        view.EmployeeMembershipId,
        view.EmployeeName,
        view.EmployeeNumber,
        view.PeriodStart,
        view.PeriodEnd,
        view.TimeZoneId,
        view.IsScheduleConfigured,
        view.ScheduledWorkdayCount,
        view.CompletedShiftCount,
        view.OpenShiftCount,
        view.MissingWorkdayCount,
        view.PendingCorrectionCount,
        view.TotalWorkedMinutes,
        view.PendingCorrectionMinutes,
        view.Days.Select(day => new MonthlyAttendanceReportDayResponse(
            day.Date,
            day.IsWorkday,
            day.Status,
            day.Shifts.Select(shift => new MonthlyAttendanceReportShiftResponse(
                shift.WorkShiftId,
                shift.WorkDate,
                shift.ClockInAtLocal,
                shift.ClockInTimeZoneId,
                shift.ClockOutAtLocal,
                shift.ClockOutTimeZoneId,
                shift.WorkedMinutes,
                shift.IsOpen,
                shift.WorkedSegmentStartAtLocal,
                shift.WorkedSegmentEndAtLocal,
                shift.IsContinuation)).ToArray(),
            day.PendingCorrection is { } pending
                ? new MonthlyAttendancePendingCorrectionResponse(
                    pending.ClockInAtLocal,
                    pending.ClockOutAtLocal,
                    pending.TimeZoneId,
                    pending.WorkedMinutes)
                : null)).ToArray());

}

public sealed record MonthlyAttendanceReportResponse(
    Guid EmployeeMembershipId,
    string EmployeeName,
    string? EmployeeNumber,
    DateOnly PeriodStart,
    DateOnly PeriodEnd,
    string TimeZoneId,
    bool IsScheduleConfigured,
    int ScheduledWorkdayCount,
    int CompletedShiftCount,
    int OpenShiftCount,
    int MissingWorkdayCount,
    int PendingCorrectionCount,
    int TotalWorkedMinutes,
    int PendingCorrectionMinutes,
    IReadOnlyList<MonthlyAttendanceReportDayResponse> Days);

public sealed record MonthlyAttendanceTeamMemberResponse(
    Guid EmployeeMembershipId,
    string EmployeeName,
    string? EmployeeNumber);

public sealed record MonthlyAttendanceReportDayResponse(
    DateOnly Date,
    bool IsWorkday,
    string Status,
    IReadOnlyList<MonthlyAttendanceReportShiftResponse> Shifts,
    MonthlyAttendancePendingCorrectionResponse? PendingCorrection);

public sealed record MonthlyAttendancePendingCorrectionResponse(
    DateTimeOffset? ClockInAtLocal,
    DateTimeOffset? ClockOutAtLocal,
    string TimeZoneId,
    int? WorkedMinutes);

public sealed record MonthlyAttendanceReportShiftResponse(
    Guid WorkShiftId,
    DateOnly WorkDate,
    DateTimeOffset ClockInAtLocal,
    string ClockInTimeZoneId,
    DateTimeOffset? ClockOutAtLocal,
    string? ClockOutTimeZoneId,
    int? WorkedMinutes,
    bool IsOpen,
    DateTimeOffset WorkedSegmentStartAtLocal,
    DateTimeOffset? WorkedSegmentEndAtLocal,
    bool IsContinuation);

public sealed record MonthlyAttendanceReportErrorResponse(string Error);
