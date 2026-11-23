using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Personal.Attendance.Api.Authentication;
using Personal.Attendance.Application.Invitations;

namespace Personal.Attendance.Api.Controllers;

[ApiController]
[Route("api/employee-invitations")]
public sealed class EmployeeInvitationsController(IEmployeeInvitationService invitationService) : ControllerBase
{
    [HttpGet("managers")]
    [Authorize(
        AuthenticationSchemes = AttendanceAuthenticationDefaults.SchemeName,
        Roles = "Admin,HR,PayrollRepresentative")]
    [ProducesResponseType<IReadOnlyList<EmployeeInvitationManagerResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<IReadOnlyList<EmployeeInvitationManagerResponse>>> GetManagers(
        CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(User.FindFirstValue(AttendanceAuthenticationDefaults.CompanyIdClaim), out var companyId) ||
            !Guid.TryParse(User.FindFirstValue(AttendanceAuthenticationDefaults.MembershipIdClaim), out var membershipId))
        {
            return Unauthorized(new ErrorResponse("The current session does not identify an active company membership."));
        }

        var result = await invitationService.GetManagersAsync(companyId, membershipId, cancellationToken);
        return result.Status switch
        {
            EmployeeInvitationOperationStatus.Succeeded =>
                Ok(result.Managers.Select(manager => new EmployeeInvitationManagerResponse(
                    manager.MembershipId,
                    manager.DisplayName,
                    manager.EmployeeNumber)).ToArray()),
            EmployeeInvitationOperationStatus.Forbidden =>
                StatusCode(StatusCodes.Status403Forbidden, new ErrorResponse("You cannot create company invitations.")),
            _ => StatusCode(StatusCodes.Status503ServiceUnavailable, new ErrorResponse("The employee list is temporarily unavailable."))
        };
    }

    [HttpPost]
    [Authorize(
        AuthenticationSchemes = AttendanceAuthenticationDefaults.SchemeName,
        Roles = "Admin,HR,PayrollRepresentative")]
    [ValidateAntiForgeryToken]
    [ProducesResponseType<EmployeeInvitationCreatedResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<EmployeeInvitationCreatedResponse>> Create(
        CreateEmployeeInvitationRequest request,
        CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(User.FindFirstValue(AttendanceAuthenticationDefaults.CompanyIdClaim), out var companyId) ||
            !Guid.TryParse(User.FindFirstValue(AttendanceAuthenticationDefaults.MembershipIdClaim), out var membershipId))
        {
            return Unauthorized(new ErrorResponse("The current session does not identify an active company membership."));
        }

        var result = await invitationService.CreateAsync(
            new CreateEmployeeInvitationCommand(
                companyId,
                membershipId,
                request.Email,
                request.DisplayName,
                request.ManagerMembershipId,
                request.Roles),
            cancellationToken);

        return result.Status switch
        {
            EmployeeInvitationOperationStatus.Succeeded when result.Invitation is not null =>
                StatusCode(StatusCodes.Status201Created, new EmployeeInvitationCreatedResponse(
                    result.Invitation.InvitationId,
                    result.Invitation.ActivationUrl,
                    result.Invitation.ExpiresAtUtc)),
            EmployeeInvitationOperationStatus.Forbidden =>
                StatusCode(StatusCodes.Status403Forbidden, new ErrorResponse("You cannot create this invitation or assign these roles.")),
            EmployeeInvitationOperationStatus.Conflict =>
                Conflict(new ErrorResponse("An employee or invitation with these company details already exists.")),
            EmployeeInvitationOperationStatus.ManagerRequired =>
                BadRequest(new ErrorResponse("An employee's direct manager must be selected when creating the invitation.")),
            _ => StatusCode(StatusCodes.Status503ServiceUnavailable, new ErrorResponse("Invitation service is temporarily unavailable."))
        };
    }

    [HttpPost("validate")]
    [AllowAnonymous]
    [ProducesResponseType<EmployeeInvitationDetailsResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<EmployeeInvitationDetailsResponse>> Validate(
        ValidateEmployeeInvitationRequest request,
        CancellationToken cancellationToken)
    {
        var result = await invitationService.ValidateAsync(request.Token, cancellationToken);
        if (result.Status == EmployeeInvitationOperationStatus.Unavailable)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new ErrorResponse("Invitation service is temporarily unavailable."));
        }

        if (result.Status != EmployeeInvitationOperationStatus.Succeeded || result.Details is null)
        {
            return NotFound(new ErrorResponse("This invitation is invalid, expired, or already used."));
        }

        return Ok(new EmployeeInvitationDetailsResponse(
            result.Details.Email,
            result.Details.DisplayName,
            result.Details.ExpiresAtUtc,
            result.Details.CompanyCode));
    }

    [HttpPost("accept")]
    [AllowAnonymous]
    [ValidateAntiForgeryToken]
    [ProducesResponseType<EmployeeInvitationAcceptedResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<EmployeeInvitationAcceptedResponse>> Accept(
        AcceptEmployeeInvitationRequest request,
        CancellationToken cancellationToken)
    {
        Guid? authenticatedUserId = Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId)
            ? userId
            : null;

        var result = await invitationService.AcceptAsync(
            request.Token,
            request.Password,
            authenticatedUserId,
            cancellationToken);

        return result.Status switch
        {
            EmployeeInvitationOperationStatus.Succeeded =>
                Ok(new EmployeeInvitationAcceptedResponse(result.CompanyCode!)),
            EmployeeInvitationOperationStatus.InvalidOrExpired =>
                NotFound(new ErrorResponse("This invitation is invalid, expired, or already used.")),
            EmployeeInvitationOperationStatus.ExistingAccountMustSignIn =>
                Conflict(new ErrorResponse("Sign in to the existing account that matches this invitation, then accept it again.")),
            EmployeeInvitationOperationStatus.PasswordRequired =>
                BadRequest(new ErrorResponse("A password is required to activate a new or unconfirmed account.")),
            EmployeeInvitationOperationStatus.InvalidPassword =>
                BadRequest(new ErrorResponse(string.Join(" ", result.Errors))),
            EmployeeInvitationOperationStatus.Conflict =>
                Conflict(new ErrorResponse("The invitation conflicts with an existing company account or employee record.")),
            _ => StatusCode(StatusCodes.Status503ServiceUnavailable, new ErrorResponse("Invitation service is temporarily unavailable."))
        };
    }
}

public sealed class CreateEmployeeInvitationRequest
{
    [Required]
    [EmailAddress]
    [StringLength(256)]
    public string Email { get; init; } = string.Empty;

    [Required]
    [StringLength(200, MinimumLength = 1)]
    public string DisplayName { get; init; } = string.Empty;

    [Required]
    public Guid? ManagerMembershipId { get; init; }

    [Required]
    [MinLength(1)]
    public string[] Roles { get; init; } = [];
}

public sealed class ValidateEmployeeInvitationRequest
{
    [Required]
    [StringLength(128, MinimumLength = 20)]
    public string Token { get; init; } = string.Empty;
}

public sealed class AcceptEmployeeInvitationRequest
{
    [Required]
    [StringLength(128, MinimumLength = 20)]
    public string Token { get; init; } = string.Empty;

    [StringLength(256, MinimumLength = 1)]
    public string? Password { get; init; }
}

public sealed record EmployeeInvitationCreatedResponse(
    Guid InvitationId,
    string ActivationUrl,
    DateTimeOffset ExpiresAtUtc);

public sealed record EmployeeInvitationManagerResponse(Guid MembershipId, string DisplayName, string? EmployeeNumber);

public sealed record EmployeeInvitationDetailsResponse(
    string Email,
    string DisplayName,
    DateTimeOffset ExpiresAtUtc,
    string CompanyCode);

public sealed record EmployeeInvitationAcceptedResponse(string CompanyCode);
