using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Personal.Attendance.Api.Authentication;
using Personal.Attendance.Application.Attendance;

namespace Personal.Attendance.Api.Controllers;

[ApiController]
[Route("api/attendance/corrections")]
[Authorize(AuthenticationSchemes = AttendanceAuthenticationDefaults.SchemeName)]
public sealed class AttendanceCorrectionsController(
    IAttendanceCorrectionService correctionService,
    ILogger<AttendanceCorrectionsController> logger) : ControllerBase
{
    [HttpPost]
    [ValidateAntiForgeryToken]
    [ProducesResponseType<AttendanceCorrectionCreatedResponse>(StatusCodes.Status202Accepted)]
    [ProducesResponseType<AttendanceCorrectionErrorResponse>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<AttendanceCorrectionErrorResponse>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<AttendanceCorrectionErrorResponse>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<AttendanceCorrectionErrorResponse>(StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<AttendanceCorrectionCreatedResponse>> Submit(
        SubmitAttendanceCorrectionRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actor)) return Unauthorized();

        try
        {
            var requestId = await correctionService.SubmitAsync(
                new SubmitAttendanceCorrectionCommand(
                    actor,
                    request.WorkDate!.Value,
                    request.ClockInLocalTime,
                    request.ClockOutLocalTime,
                    request.ClockOutNextDay),
                cancellationToken);

            return Accepted(new AttendanceCorrectionCreatedResponse(requestId, "PendingApproval"));
        }
        catch (AttendanceCorrectionAccessDeniedException exception)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new AttendanceCorrectionErrorResponse(exception.Message));
        }
        catch (AttendanceCorrectionConflictException exception)
        {
            return Conflict(new AttendanceCorrectionErrorResponse(exception.Message));
        }
        catch (ArgumentException exception)
        {
            return BadRequest(new AttendanceCorrectionErrorResponse(exception.Message));
        }
        catch (AttendanceCorrectionUnavailableException)
        {
            logger.LogWarning("An attendance correction request could not be submitted.");
            return StatusCode(StatusCodes.Status503ServiceUnavailable,
                new AttendanceCorrectionErrorResponse("Attendance corrections are temporarily unavailable."));
        }
    }

    [HttpGet("pending")]
    [ProducesResponseType<IReadOnlyList<AttendanceCorrectionResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<AttendanceCorrectionErrorResponse>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<AttendanceCorrectionErrorResponse>(StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<IReadOnlyList<AttendanceCorrectionResponse>>> GetPending(
        CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actor)) return Unauthorized();

        try
        {
            var requests = await correctionService.GetPendingAsync(actor, cancellationToken);
            return Ok(requests.Select(ToResponse).ToArray());
        }
        catch (AttendanceCorrectionAccessDeniedException exception)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new AttendanceCorrectionErrorResponse(exception.Message));
        }
        catch (AttendanceCorrectionUnavailableException)
        {
            logger.LogWarning("Pending attendance corrections could not be loaded.");
            return StatusCode(StatusCodes.Status503ServiceUnavailable,
                new AttendanceCorrectionErrorResponse("Attendance corrections are temporarily unavailable."));
        }
    }

    [HttpPost("{requestId:guid}/approve")]
    [ValidateAntiForgeryToken]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<AttendanceCorrectionErrorResponse>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<AttendanceCorrectionErrorResponse>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<AttendanceCorrectionErrorResponse>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<AttendanceCorrectionErrorResponse>(StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> Approve(Guid requestId, CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actor)) return Unauthorized();

        try
        {
            await correctionService.ApproveAsync(actor, requestId, cancellationToken);
            return NoContent();
        }
        catch (AttendanceCorrectionAccessDeniedException exception)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new AttendanceCorrectionErrorResponse(exception.Message));
        }
        catch (AttendanceCorrectionNotFoundException exception)
        {
            return NotFound(new AttendanceCorrectionErrorResponse(exception.Message));
        }
        catch (AttendanceCorrectionConflictException exception)
        {
            return Conflict(new AttendanceCorrectionErrorResponse(exception.Message));
        }
        catch (AttendanceCorrectionUnavailableException)
        {
            logger.LogWarning("An attendance correction could not be approved.");
            return StatusCode(StatusCodes.Status503ServiceUnavailable,
                new AttendanceCorrectionErrorResponse("Attendance corrections are temporarily unavailable."));
        }
    }

    [HttpPost("{requestId:guid}/reject")]
    [ValidateAntiForgeryToken]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<AttendanceCorrectionErrorResponse>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<AttendanceCorrectionErrorResponse>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<AttendanceCorrectionErrorResponse>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<AttendanceCorrectionErrorResponse>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<AttendanceCorrectionErrorResponse>(StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> Reject(
        Guid requestId,
        RejectAttendanceCorrectionRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actor)) return Unauthorized();

        try
        {
            await correctionService.RejectAsync(actor, requestId, request.Reason, cancellationToken);
            return NoContent();
        }
        catch (AttendanceCorrectionAccessDeniedException exception)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new AttendanceCorrectionErrorResponse(exception.Message));
        }
        catch (AttendanceCorrectionNotFoundException exception)
        {
            return NotFound(new AttendanceCorrectionErrorResponse(exception.Message));
        }
        catch (AttendanceCorrectionConflictException exception)
        {
            return Conflict(new AttendanceCorrectionErrorResponse(exception.Message));
        }
        catch (ArgumentException exception)
        {
            return BadRequest(new AttendanceCorrectionErrorResponse(exception.Message));
        }
        catch (AttendanceCorrectionUnavailableException)
        {
            logger.LogWarning("An attendance correction could not be rejected.");
            return StatusCode(StatusCodes.Status503ServiceUnavailable,
                new AttendanceCorrectionErrorResponse("Attendance corrections are temporarily unavailable."));
        }
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

    private static AttendanceCorrectionResponse ToResponse(AttendanceCorrectionSummary request) => new(
        request.RequestId,
        request.EmployeeName,
        request.EmployeeNumber,
        request.WorkDate,
        request.TimeZoneId,
        request.RequestedClockInAtLocal,
        request.RequestedClockOutAtLocal,
        request.SubmittedAtLocal,
        request.Status,
        request.RejectionReason,
        request.IsAttentionRequired);
}

public sealed class SubmitAttendanceCorrectionRequest
{
    [Required]
    public DateOnly? WorkDate { get; init; }
    public TimeOnly? ClockInLocalTime { get; init; }
    public TimeOnly? ClockOutLocalTime { get; init; }
    public bool ClockOutNextDay { get; init; }
}

public sealed class RejectAttendanceCorrectionRequest
{
    [Required, StringLength(1000, MinimumLength = 1)]
    public string Reason { get; init; } = string.Empty;
}

public sealed record AttendanceCorrectionCreatedResponse(Guid RequestId, string Status);

public sealed record AttendanceCorrectionResponse(
    Guid RequestId,
    string EmployeeName,
    string? EmployeeNumber,
    DateOnly WorkDate,
    string TimeZoneId,
    DateTimeOffset? RequestedClockInAtLocal,
    DateTimeOffset? RequestedClockOutAtLocal,
    DateTimeOffset SubmittedAtLocal,
    string Status,
    string? RejectionReason,
    bool IsAttentionRequired);

public sealed record AttendanceCorrectionErrorResponse(string Error);
