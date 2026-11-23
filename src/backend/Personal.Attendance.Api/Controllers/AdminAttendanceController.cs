using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Personal.Attendance.Api.Authentication;
using Personal.Attendance.Application.Attendance;

namespace Personal.Attendance.Api.Controllers;

[ApiController]
[Route("api/admin/attendance")]
[Authorize(
    AuthenticationSchemes = AttendanceAuthenticationDefaults.SchemeName,
    Roles = "Admin")]
public sealed class AdminAttendanceController(
    IAdminAttendanceService attendanceService,
    ILogger<AdminAttendanceController> logger) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<AdminAttendanceMonthResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<AdminAttendanceErrorResponse>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<AdminAttendanceErrorResponse>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<AdminAttendanceErrorResponse>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<AdminAttendanceErrorResponse>(StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<AdminAttendanceMonthResponse>> GetMonth(
        [FromQuery, Required] Guid employeeMembershipId,
        [FromQuery, Range(2000, 9999)] int year,
        [FromQuery, Range(1, 12)] int month,
        CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actor)) return Unauthorized();

        try
        {
            var result = await attendanceService.GetMonthAsync(
                actor, employeeMembershipId, year, month, cancellationToken);
            return Ok(ToResponse(result));
        }
        catch (AdminAttendanceAccessDeniedException exception)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new AdminAttendanceErrorResponse(exception.Message));
        }
        catch (AdminAttendanceNotFoundException exception)
        {
            return NotFound(new AdminAttendanceErrorResponse(exception.Message));
        }
        catch (ArgumentOutOfRangeException exception)
        {
            return BadRequest(new AdminAttendanceErrorResponse(exception.Message));
        }
        catch (AdminAttendanceUnavailableException)
        {
            logger.LogWarning("The Admin attendance month could not be loaded.");
            return StatusCode(StatusCodes.Status503ServiceUnavailable,
                new AdminAttendanceErrorResponse("Attendance data is temporarily unavailable."));
        }
    }

    [HttpPut("{workShiftId:guid}")]
    [ValidateAntiForgeryToken]
    [ProducesResponseType<AdminAttendanceChangeResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<AdminAttendanceErrorResponse>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<AdminAttendanceErrorResponse>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<AdminAttendanceErrorResponse>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<AdminAttendanceErrorResponse>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<AdminAttendanceErrorResponse>(StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<AdminAttendanceChangeResponse>> UpdateShift(
        Guid workShiftId,
        UpdateAdminAttendanceRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actor)) return Unauthorized();

        try
        {
            var change = await attendanceService.UpdateShiftAsync(
                actor,
                workShiftId,
                request.ClockInLocalDateTime,
                request.ClockOutLocalDateTime,
                request.Reason,
                cancellationToken);
            return Ok(ToResponse(change));
        }
        catch (AdminAttendanceAccessDeniedException exception)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new AdminAttendanceErrorResponse(exception.Message));
        }
        catch (AdminAttendanceNotFoundException exception)
        {
            return NotFound(new AdminAttendanceErrorResponse(exception.Message));
        }
        catch (AdminAttendanceConflictException exception)
        {
            return Conflict(new AdminAttendanceErrorResponse(exception.Message));
        }
        catch (ArgumentException exception)
        {
            return BadRequest(new AdminAttendanceErrorResponse(exception.Message));
        }
        catch (AdminAttendanceUnavailableException)
        {
            logger.LogWarning("An Admin attendance change could not be saved.");
            return StatusCode(StatusCodes.Status503ServiceUnavailable,
                new AdminAttendanceErrorResponse("Attendance data is temporarily unavailable."));
        }
    }

    private bool TryGetActor(out AdminAttendanceActor actor)
    {
        if (!Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId) ||
            !Guid.TryParse(User.FindFirstValue(AttendanceAuthenticationDefaults.CompanyIdClaim), out var companyId) ||
            !Guid.TryParse(User.FindFirstValue(AttendanceAuthenticationDefaults.MembershipIdClaim), out var membershipId))
        {
            actor = null!;
            return false;
        }

        actor = new AdminAttendanceActor(userId, companyId, membershipId);
        return true;
    }

    private static AdminAttendanceMonthResponse ToResponse(AdminAttendanceMonth month) => new(
        month.EmployeeMembershipId,
        month.Year,
        month.Month,
        month.Shifts.Select(shift => new AdminAttendanceShiftResponse(
            shift.WorkShiftId,
            shift.WorkDate,
            shift.EmployeeName,
            shift.EmployeeNumber,
            shift.ClockInAtLocal,
            shift.ClockInTimeZoneId,
            shift.ClockInSource,
            shift.ClockOutAtLocal,
            shift.ClockOutTimeZoneId,
            shift.ClockOutSource)).ToArray(),
        month.Changes.Select(ToResponse).ToArray());

    private static AdminAttendanceChangeResponse ToResponse(AdminAttendanceChange change) => new(
        change.AuditId,
        change.ChangedByMembershipId,
        change.ChangedByName,
        change.PreviousWorkDate,
        change.WorkDate,
        change.EmployeeName,
        change.EmployeeNumber,
        change.PreviousClockInAtLocal,
        change.PreviousClockInTimeZoneId,
        change.PreviousClockInSource,
        change.PreviousClockInProvider,
        change.PreviousClockInProviderSampleUtc,
        change.ClockInAtLocal,
        change.ClockInTimeZoneId,
        change.ClockInSource,
        change.ClockInProvider,
        change.ClockInProviderSampleUtc,
        change.PreviousClockOutAtLocal,
        change.PreviousClockOutTimeZoneId,
        change.PreviousClockOutSource,
        change.PreviousClockOutProvider,
        change.PreviousClockOutProviderSampleUtc,
        change.ClockOutAtLocal,
        change.ClockOutTimeZoneId,
        change.ClockOutSource,
        change.ClockOutProvider,
        change.ClockOutProviderSampleUtc,
        change.Reason,
        change.ChangedAtLocal,
        change.ChangeTimeZoneId,
        change.ChangeTimeProvider,
        change.ChangeProviderSampleUtc);
}

public sealed class UpdateAdminAttendanceRequest
{
    [Required]
    public DateTime ClockInLocalDateTime { get; init; }

    public DateTime? ClockOutLocalDateTime { get; init; }

    [Required]
    [StringLength(1000, MinimumLength = 1)]
    public string Reason { get; init; } = string.Empty;
}

public sealed record AdminAttendanceMonthResponse(
    Guid EmployeeMembershipId,
    int Year,
    int Month,
    IReadOnlyList<AdminAttendanceShiftResponse> Shifts,
    IReadOnlyList<AdminAttendanceChangeResponse> Changes);

public sealed record AdminAttendanceShiftResponse(
    Guid WorkShiftId,
    DateOnly WorkDate,
    string EmployeeName,
    string? EmployeeNumber,
    DateTimeOffset ClockInAtLocal,
    string ClockInTimeZoneId,
    string ClockInSource,
    DateTimeOffset? ClockOutAtLocal,
    string? ClockOutTimeZoneId,
    string? ClockOutSource);

public sealed record AdminAttendanceChangeResponse(
    Guid AuditId,
    Guid ChangedByMembershipId,
    string ChangedByName,
    DateOnly PreviousWorkDate,
    DateOnly WorkDate,
    string EmployeeName,
    string? EmployeeNumber,
    DateTimeOffset PreviousClockInAtLocal,
    string PreviousClockInTimeZoneId,
    string PreviousClockInSource,
    string? PreviousClockInProvider,
    DateTimeOffset? PreviousClockInProviderSampleUtc,
    DateTimeOffset ClockInAtLocal,
    string ClockInTimeZoneId,
    string ClockInSource,
    string? ClockInProvider,
    DateTimeOffset? ClockInProviderSampleUtc,
    DateTimeOffset? PreviousClockOutAtLocal,
    string? PreviousClockOutTimeZoneId,
    string? PreviousClockOutSource,
    string? PreviousClockOutProvider,
    DateTimeOffset? PreviousClockOutProviderSampleUtc,
    DateTimeOffset? ClockOutAtLocal,
    string? ClockOutTimeZoneId,
    string? ClockOutSource,
    string? ClockOutProvider,
    DateTimeOffset? ClockOutProviderSampleUtc,
    string Reason,
    DateTimeOffset ChangedAtLocal,
    string ChangeTimeZoneId,
    string ChangeTimeProvider,
    DateTimeOffset ChangeProviderSampleUtc);

public sealed record AdminAttendanceErrorResponse(string Error);
