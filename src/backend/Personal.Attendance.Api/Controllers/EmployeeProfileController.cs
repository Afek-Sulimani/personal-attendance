using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Personal.Attendance.Api.Authentication;
using Personal.Attendance.Application.Profiles;
using Personal.Attendance.Domain.Enums;

namespace Personal.Attendance.Api.Controllers;

[ApiController]
[Route("api/profile")]
[Authorize(AuthenticationSchemes = AttendanceAuthenticationDefaults.SchemeName)]
public sealed class EmployeeProfileController(
    IEmployeeProfileService profileService,
    MemoryCacheTicketStore ticketStore,
    ILogger<EmployeeProfileController> logger) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<EmployeeProfileResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProfileErrorResponse>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProfileErrorResponse>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProfileErrorResponse>(StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<EmployeeProfileResponse>> GetMyProfile(CancellationToken cancellationToken)
    {
        if (!TryGetCompanyActor(out var actor)) return Unauthorized();

        try
        {
            return Ok(ToResponse(await profileService.GetMyProfileAsync(actor, cancellationToken)));
        }
        catch (EmployeeProfileAccessDeniedException exception)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new ProfileErrorResponse(exception.Message));
        }
        catch (EmployeeProfileNotFoundException exception)
        {
            return NotFound(new ProfileErrorResponse(exception.Message));
        }
        catch (EmployeeProfileUnavailableException)
        {
            logger.LogWarning("The current employee profile could not be loaded.");
            return StatusCode(StatusCodes.Status503ServiceUnavailable,
                new ProfileErrorResponse("Profile information is temporarily unavailable."));
        }
    }

    [HttpPost("password")]
    [ValidateAntiForgeryToken]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProfileErrorResponse>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProfileErrorResponse>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProfileErrorResponse>(StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> ChangePassword(
        ChangePasswordRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryGetCompanyActor(out var actor)) return Unauthorized();

        try
        {
            var result = await profileService.ChangePasswordAsync(
                actor,
                request.CurrentPassword,
                request.NewPassword,
                cancellationToken);

            switch (result.Status)
            {
                case PasswordChangeStatus.Succeeded:
                    await ticketStore.RemoveAllForUserAsync(actor.UserId);
                    await HttpContext.SignOutAsync(AttendanceAuthenticationDefaults.SchemeName);
                    return NoContent();
                case PasswordChangeStatus.CurrentPasswordInvalid:
                    return BadRequest(new ProfileErrorResponse("The current password is incorrect."));
                case PasswordChangeStatus.NewPasswordInvalid:
                    return BadRequest(new ProfileErrorResponse(
                        result.Errors.Count == 0
                            ? "The new password does not meet the account requirements."
                            : string.Join(" ", result.Errors)));
                default:
                    return Unauthorized();
            }
        }
        catch (EmployeeProfileAccessDeniedException exception)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new ProfileErrorResponse(exception.Message));
        }
        catch (EmployeeProfileUnavailableException)
        {
            logger.LogWarning("The current user's password could not be changed.");
            return StatusCode(StatusCodes.Status503ServiceUnavailable,
                new ProfileErrorResponse("The password could not be changed right now."));
        }
    }

    [HttpGet("company-employees")]
    [Authorize(Roles = "Admin,HR,PayrollRepresentative")]
    [ProducesResponseType<IReadOnlyList<ManagedEmployeeProfileResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProfileErrorResponse>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProfileErrorResponse>(StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<IReadOnlyList<ManagedEmployeeProfileResponse>>> GetCompanyEmployees(
        CancellationToken cancellationToken)
    {
        if (!TryGetCompanyActor(out var actor)) return Unauthorized();

        try
        {
            var employees = await profileService.GetCompanyEmployeesAsync(actor, cancellationToken);
            return Ok(employees.Select(ToResponse).ToArray());
        }
        catch (EmployeeProfileAccessDeniedException exception)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new ProfileErrorResponse(exception.Message));
        }
        catch (EmployeeProfileUnavailableException)
        {
            logger.LogWarning("The company employee profile list could not be loaded.");
            return StatusCode(StatusCodes.Status503ServiceUnavailable,
                new ProfileErrorResponse("Company employee profiles are temporarily unavailable."));
        }
    }

    [HttpPut("company-employees/{employeeMembershipId:guid}/compensation")]
    [Authorize(Roles = "Admin,HR,PayrollRepresentative")]
    [ValidateAntiForgeryToken]
    [ProducesResponseType<ManagedEmployeeProfileResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProfileErrorResponse>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProfileErrorResponse>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProfileErrorResponse>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProfileErrorResponse>(StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<ManagedEmployeeProfileResponse>> UpdateCompensation(
        Guid employeeMembershipId,
        UpdateEmployeeCompensationRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryGetCompanyActor(out var actor)) return Unauthorized();
        if (!Enum.TryParse<EmployeeCompensationType>(request.CompensationType, true, out var compensationType) ||
            !Enum.IsDefined(compensationType))
        {
            return BadRequest(new ProfileErrorResponse("Choose Global or Hourly as the compensation type."));
        }

        try
        {
            var employee = await profileService.UpdateCompensationAsync(
                actor,
                employeeMembershipId,
                compensationType,
                request.HourlyRate,
                cancellationToken);
            return Ok(ToResponse(employee));
        }
        catch (EmployeeProfileAccessDeniedException exception)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new ProfileErrorResponse(exception.Message));
        }
        catch (EmployeeProfileNotFoundException exception)
        {
            return NotFound(new ProfileErrorResponse(exception.Message));
        }
        catch (ArgumentException exception)
        {
            return BadRequest(new ProfileErrorResponse(exception.Message));
        }
        catch (EmployeeProfileUnavailableException)
        {
            logger.LogWarning("Employee compensation could not be saved.");
            return StatusCode(StatusCodes.Status503ServiceUnavailable,
                new ProfileErrorResponse("Employee compensation is temporarily unavailable."));
        }
    }

    private bool TryGetCompanyActor(out CompanyEmployeeActor actor)
    {
        if (!Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId) ||
            !Guid.TryParse(User.FindFirstValue(AttendanceAuthenticationDefaults.CompanyIdClaim), out var companyId) ||
            !Guid.TryParse(User.FindFirstValue(AttendanceAuthenticationDefaults.MembershipIdClaim), out var membershipId))
        {
            actor = null!;
            return false;
        }

        actor = new CompanyEmployeeActor(userId, companyId, membershipId);
        return true;
    }

    private static EmployeeProfileResponse ToResponse(EmployeeProfileDetails profile) => new(
        profile.Email,
        profile.UserName,
        profile.Companies.Select(company => new CompanyEmploymentResponse(
            company.MembershipId,
            company.CompanyName,
            company.CompanyCode,
            company.DisplayName,
            company.EmployeeNumber,
            company.Roles,
            company.CompensationType?.ToString(),
            company.HourlyRate)).ToArray());

    private static ManagedEmployeeProfileResponse ToResponse(ManagedEmployeeProfile employee) => new(
        employee.MembershipId,
        employee.DisplayName,
        employee.Email,
        employee.EmployeeNumber,
        employee.CompensationType?.ToString(),
        employee.HourlyRate);
}

public sealed class ChangePasswordRequest
{
    [Required]
    [StringLength(256, MinimumLength = 1)]
    public string CurrentPassword { get; init; } = string.Empty;

    [Required]
    [StringLength(256, MinimumLength = 6)]
    public string NewPassword { get; init; } = string.Empty;
}

public sealed class UpdateEmployeeCompensationRequest
{
    [Required]
    public string CompensationType { get; init; } = string.Empty;

    public decimal? HourlyRate { get; init; }
}

public sealed record EmployeeProfileResponse(
    string Email,
    string UserName,
    IReadOnlyList<CompanyEmploymentResponse> Companies);

public sealed record CompanyEmploymentResponse(
    Guid MembershipId,
    string CompanyName,
    string CompanyCode,
    string DisplayName,
    string? EmployeeNumber,
    IReadOnlyList<string> Roles,
    string? CompensationType,
    decimal? HourlyRate);

public sealed record ManagedEmployeeProfileResponse(
    Guid MembershipId,
    string DisplayName,
    string Email,
    string? EmployeeNumber,
    string? CompensationType,
    decimal? HourlyRate);

public sealed record ProfileErrorResponse(string Error);
