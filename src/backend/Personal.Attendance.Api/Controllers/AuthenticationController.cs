using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Personal.Attendance.Api.Authentication;
using Personal.Attendance.Application.Authentication;

namespace Personal.Attendance.Api.Controllers;

[ApiController]
[Route("api/auth")]
public sealed class AuthenticationController(
    IAntiforgery antiforgery,
    IEmployeeAuthenticationService authenticationService,
    ILogger<AuthenticationController> logger) : ControllerBase
{
    [HttpGet("antiforgery")]
    [AllowAnonymous]
    [ProducesResponseType<AntiforgeryTokenResponse>(StatusCodes.Status200OK)]
    public ActionResult<AntiforgeryTokenResponse> GetAntiforgeryToken()
    {
        var tokens = antiforgery.GetAndStoreTokens(HttpContext);
        return Ok(new AntiforgeryTokenResponse(tokens.RequestToken!));
    }

    [HttpPost("login")]
    [AllowAnonymous]
    [EnableRateLimiting(AttendanceAuthenticationDefaults.LoginRateLimitPolicyName)]
    [ValidateAntiForgeryToken]
    [ProducesResponseType<CurrentEmployeeResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<CurrentEmployeeResponse>> Login(
        LoginRequest request,
        CancellationToken cancellationToken)
    {
        AuthenticatedEmployee? employee;
        try
        {
            employee = await authenticationService.AuthenticateAsync(
                new EmployeeLoginCredentials(request.CompanyIdentifier, request.Email, request.Password),
                cancellationToken);
        }
        catch (AuthenticationUnavailableException)
        {
            logger.LogWarning("Authentication is unavailable because its data store could not be reached or configured.");
            return StatusCode(
                StatusCodes.Status503ServiceUnavailable,
                new ErrorResponse("Authentication is temporarily unavailable."));
        }

        if (employee is null)
        {
            return Unauthorized(new ErrorResponse("Invalid company name or ID, email, or password."));
        }

        await HttpContext.SignInAsync(
            AttendanceAuthenticationDefaults.SchemeName,
            EmployeePrincipal.Create(employee),
            new AuthenticationProperties { IsPersistent = false });

        return Ok(ToResponse(employee));
    }

    [HttpGet("me")]
    [Authorize(AuthenticationSchemes = AttendanceAuthenticationDefaults.SchemeName)]
    [ProducesResponseType<CurrentEmployeeResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public ActionResult<CurrentEmployeeResponse> GetCurrentEmployee()
    {
        if (!Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId) ||
            !Guid.TryParse(User.FindFirstValue(AttendanceAuthenticationDefaults.CompanyIdClaim), out var companyId) ||
            !Guid.TryParse(User.FindFirstValue(AttendanceAuthenticationDefaults.MembershipIdClaim), out var membershipId) ||
            !Guid.TryParse(User.FindFirstValue(AttendanceAuthenticationDefaults.EmployeeProfileIdClaim), out var employeeProfileId))
        {
            return Unauthorized();
        }

        var email = User.FindFirstValue(ClaimTypes.Email);
        var displayName = User.FindFirstValue(ClaimTypes.Name);
        var companyCode = User.FindFirstValue(AttendanceAuthenticationDefaults.CompanyCodeClaim);
        if (email is null || displayName is null || companyCode is null)
        {
            return Unauthorized();
        }

        return Ok(new CurrentEmployeeResponse(
            userId,
            email,
            displayName,
            companyId,
            companyCode,
            membershipId,
            employeeProfileId,
            User.FindFirstValue(AttendanceAuthenticationDefaults.EmployeeNumberClaim),
            User.FindAll(ClaimTypes.Role).Select(claim => claim.Value).ToArray()));
    }

    [HttpPost("logout")]
    [Authorize(AuthenticationSchemes = AttendanceAuthenticationDefaults.SchemeName)]
    [ValidateAntiForgeryToken]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(AttendanceAuthenticationDefaults.SchemeName);
        return NoContent();
    }

    private static CurrentEmployeeResponse ToResponse(AuthenticatedEmployee employee) => new(
        employee.UserId,
        employee.Email,
        employee.DisplayName,
        employee.CompanyId,
        employee.CompanyCode,
        employee.MembershipId,
        employee.EmployeeProfileId,
        employee.EmployeeNumber,
        employee.Roles);
}

public sealed class LoginRequest
{
    [Required]
    [StringLength(200)]
    public string CompanyIdentifier { get; init; } = string.Empty;

    [Required]
    [EmailAddress]
    [StringLength(256)]
    public string Email { get; init; } = string.Empty;

    [Required]
    public string Password { get; init; } = string.Empty;
}

public sealed record AntiforgeryTokenResponse(string RequestToken);

public sealed record CurrentEmployeeResponse(
    Guid UserId,
    string Email,
    string DisplayName,
    Guid CompanyId,
    string CompanyCode,
    Guid MembershipId,
    Guid EmployeeProfileId,
    string? EmployeeNumber,
    IReadOnlyList<string> Roles);

public sealed record ErrorResponse(string Error);
