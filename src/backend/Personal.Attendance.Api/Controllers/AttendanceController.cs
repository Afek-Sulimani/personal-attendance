using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Personal.Attendance.Api.Authentication;
using Personal.Attendance.Application.Attendance;

namespace Personal.Attendance.Api.Controllers;

// Require the configured attendance cookie scheme for every calendar and punch endpoint in this controller.
[ApiController]
[Route("api/attendance")]
[Authorize(AuthenticationSchemes = AttendanceAuthenticationDefaults.SchemeName)]
public sealed class AttendanceController(
    IAttendanceService attendanceService,
    ILogger<AttendanceController> logger) : ControllerBase
{
    [HttpGet("calendar")]
    [ProducesResponseType<AttendanceCalendarResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<AttendanceErrorResponse>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<AttendanceErrorResponse>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<AttendanceErrorResponse>(StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<AttendanceCalendarResponse>> GetCalendar(
        [FromQuery] int? year,
        [FromQuery] int? month,
        CancellationToken cancellationToken)
    {
        // Build the actor only from authenticated claims; the caller cannot choose another employee in the query.
        if (!TryGetActor(out var actor)) return Unauthorized();

        try
        {
            // Delegate time, schedule, and shift rules to the application service rather than the controller.
            var calendar = await attendanceService.GetCalendarAsync(actor, year, month, cancellationToken);

            // Convert internal calendar models into the explicit JSON contract consumed by React.
            return Ok(new AttendanceCalendarResponse(
                calendar.Year,
                calendar.Month,
                calendar.TimeZoneId,
                calendar.TodayLocalDate,
                calendar.CurrentPayrollPeriodStartDate,
                calendar.CurrentPayrollPeriodEndDate,
                calendar.CurrentLocalTime,
                calendar.ScheduleConfigured,
                calendar.Days.Select(day => new AttendanceCalendarDayResponse(
                    day.Date,
                    day.IsWorkDay,
                    day.Status.ToString(),
                    day.ClockInTimeZoneId,
                    day.ClockInAtLocal,
                    day.ClockOutTimeZoneId,
                    day.ClockOutAtLocal,
                    day.CorrectionStatus)).ToArray()));
        }
        catch (AttendanceAccessDeniedException exception)
        {
            // Report a valid login whose company membership is no longer allowed to use attendance.
            return StatusCode(
                StatusCodes.Status403Forbidden,
                new AttendanceErrorResponse(exception.Message));
        }
        catch (ArgumentException exception)
        {
            // Invalid year/month query values are client errors rather than server failures.
            return BadRequest(new AttendanceErrorResponse(exception.Message));
        }
        catch (AttendanceUnavailableException)
        {
            // Do not expose database or provider details in the public error response.
            logger.LogWarning("The monthly attendance calendar could not be loaded.");
            return StatusCode(
                StatusCodes.Status503ServiceUnavailable,
                new AttendanceErrorResponse("Attendance is temporarily unavailable."));
        }
    }

    [HttpGet("current")]
    [ProducesResponseType<AttendanceState>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    // Return whether the authenticated employee currently has an open shift.
    public Task<ActionResult<AttendanceState>> GetCurrent(
        CancellationToken cancellationToken) =>
        ExecuteAsync(attendanceService.GetCurrentAsync, cancellationToken);

    [HttpPost("clock-in")]
    [ValidateAntiForgeryToken]
    [ProducesResponseType<AttendancePunchResult>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    // The request carries no timestamp; the attendance service obtains the authoritative time itself.
    public Task<ActionResult<AttendancePunchResult>> ClockIn(
        CancellationToken cancellationToken) =>
        ExecuteAsync(attendanceService.ClockInAsync, cancellationToken);

    [HttpPost("clock-out")]
    [ValidateAntiForgeryToken]
    [ProducesResponseType<AttendancePunchResult>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    // Close the employee's existing open shift using the same server-controlled time source.
    public Task<ActionResult<AttendancePunchResult>> ClockOut(
        CancellationToken cancellationToken) =>
        ExecuteAsync(attendanceService.ClockOutAsync, cancellationToken);

    private async Task<ActionResult<TResponse>> ExecuteAsync<TResponse>(
        Func<AttendanceActor, CancellationToken, Task<TResponse>> operation,
        CancellationToken cancellationToken)
    {
        // Reject requests whose authentication ticket does not contain all required company-scoped IDs.
        if (!TryGetActor(out var actor))
        {
            return Unauthorized();
        }

        try
        {
            // Run the selected service operation and return its result as HTTP 200.
            return Ok(await operation(actor, cancellationToken));
        }
        catch (AttendanceAccessDeniedException exception)
        {
            // Convert company membership or role failures into HTTP 403.
            return StatusCode(
                StatusCodes.Status403Forbidden,
                new AttendanceErrorResponse(exception.Message));
        }
        catch (AttendanceConflictException exception)
        {
            // Use HTTP 409 for state conflicts such as duplicate punches or a missing open shift.
            return Conflict(new AttendanceErrorResponse(exception.Message));
        }
        catch (AttendanceUnavailableException)
        {
            // Use HTTP 503 when the required attendance or external-time service is unavailable.
            logger.LogWarning("An attendance operation could not be completed.");
            return StatusCode(
                StatusCodes.Status503ServiceUnavailable,
                new AttendanceErrorResponse("Attendance is temporarily unavailable."));
        }
    }

    private bool TryGetActor(out AttendanceActor actor)
    {
        // Read identity and company scope from the authenticated ticket, never from request data.
        if (!Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId) ||
            !Guid.TryParse(User.FindFirstValue(AttendanceAuthenticationDefaults.CompanyIdClaim), out var companyId) ||
            !Guid.TryParse(User.FindFirstValue(AttendanceAuthenticationDefaults.MembershipIdClaim), out var membershipId) ||
            !Guid.TryParse(User.FindFirstValue(AttendanceAuthenticationDefaults.EmployeeProfileIdClaim), out var employeeProfileId))
        {
            actor = null!;
            return false;
        }

        // Bundle verified claim IDs for the application service's membership recheck.
        actor = new AttendanceActor(userId, companyId, membershipId, employeeProfileId);
        return true;
    }
}

public sealed record AttendanceErrorResponse(string Error);

public sealed record AttendanceCalendarResponse(
    int Year,
    int Month,
    string TimeZoneId,
    DateOnly TodayLocalDate,
    DateOnly? CurrentPayrollPeriodStartDate,
    DateOnly? CurrentPayrollPeriodEndDate,
    DateTimeOffset CurrentLocalTime,
    bool ScheduleConfigured,
    IReadOnlyList<AttendanceCalendarDayResponse> Days);

public sealed record AttendanceCalendarDayResponse(
    DateOnly Date,
    bool? IsWorkDay,
    string Status,
    string? ClockInTimeZoneId,
    DateTimeOffset? ClockInAtLocal,
    string? ClockOutTimeZoneId,
    DateTimeOffset? ClockOutAtLocal,
    string? CorrectionStatus);
