using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Personal.Attendance.Api.Authentication;
using Personal.Attendance.Application.Authentication;
using Personal.Attendance.Infrastructure.Persistence;
using Personal.Attendance.Domain.Enums;

namespace Personal.Attendance.Api.Controllers;

[ApiController]
[Route("api/auth/google")]
public sealed class GoogleAuthenticationController(IConfiguration configuration) : ControllerBase
{
    [AllowAnonymous, HttpGet("status")]
    public IActionResult Status() => Ok(new { enabled = Enabled });
    private bool Enabled => !string.IsNullOrWhiteSpace(configuration["Authentication:Google:ClientId"])
        && !string.IsNullOrWhiteSpace(configuration["Authentication:Google:ClientSecret"]);

    [AllowAnonymous, HttpGet("start")]
    public IActionResult Start([FromQuery] string companyIdentifier)
    {
        if (!Enabled) return StatusCode(503, new { error = "Google sign-in is not configured." });
        if (string.IsNullOrWhiteSpace(companyIdentifier) || companyIdentifier.Length > 200) return BadRequest();
        var properties = new AuthenticationProperties { RedirectUri = "/api/auth/google/complete" };
        properties.Items["company"] = companyIdentifier.Trim().ToUpperInvariant();
        return Challenge(properties, "Google");
    }

    [AllowAnonymous, HttpGet("complete")]
    public async Task<IActionResult> Complete(CancellationToken cancellationToken)
    {
        if (!Enabled) return NotFound();
        var external = await HttpContext.AuthenticateAsync("GoogleExternal");
        await HttpContext.SignOutAsync("GoogleExternal");
        var email = external.Principal?.FindFirstValue(ClaimTypes.Email);
        if (!external.Succeeded || email is null || external.Properties is null ||
            !external.Properties.Items.TryGetValue("company", out var company)) return Unauthorized();
        var users = HttpContext.RequestServices.GetRequiredService<UserManager<PersonalUser>>();
        var user = await users.FindByEmailAsync(email);
        if (user is null || !user.EmailConfirmed) return Redirect("/login?googleError=account-not-active");
        var db = HttpContext.RequestServices.GetRequiredService<PersonalAttendanceDbContext>();
        var companies = await db.Companies.AsNoTracking().Where(c => c.IsActive && c.NormalizedCompanyCode == company).Select(c => c.Id).ToListAsync(cancellationToken);
        if (companies.Count == 0) companies = await db.Companies.AsNoTracking().Where(c => c.IsActive && c.Name.ToUpper() == company).Select(c => c.Id).Take(2).ToListAsync(cancellationToken);
        if (companies.Count != 1) return Redirect("/login?googleError=account-not-active");
        var membership = await db.CompanyMemberships.AsNoTracking().Include(m => m.Company).Include(m => m.EmployeeProfile).Include(m => m.Roles)
            .SingleOrDefaultAsync(m => m.UserId == user.Id && m.CompanyId == companies[0] && m.IsActive, cancellationToken);
        if (membership?.EmployeeProfile is not { } profile) return Redirect("/login?googleError=account-not-active");
        var employee = new AuthenticatedEmployee(user.Id, user.Email!, profile.DisplayName, membership.CompanyId,
            membership.Company.CompanyCode, membership.MembershipId, profile.EmployeeProfileId, profile.EmployeeNumber,
            membership.Roles.Select(r => r.Role == CompanyRole.Hr ? "HR" : r.Role.ToString()).ToArray());
        await HttpContext.SignInAsync(AttendanceAuthenticationDefaults.SchemeName, EmployeePrincipal.Create(employee), new AuthenticationProperties { IsPersistent = false });
        return Redirect("/attendance");
    }
}
