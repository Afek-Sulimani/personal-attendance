using System.Security.Claims;
using Personal.Attendance.Application.Authentication;
namespace Personal.Attendance.Api.Authentication;
public static class EmployeePrincipal
{
    public static ClaimsPrincipal Create(AuthenticatedEmployee employee)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, employee.UserId.ToString()),
            new(ClaimTypes.Name, employee.DisplayName),
            new(ClaimTypes.Email, employee.Email),
            new(AttendanceAuthenticationDefaults.CompanyIdClaim, employee.CompanyId.ToString()),
            new(AttendanceAuthenticationDefaults.CompanyCodeClaim, employee.CompanyCode),
            new(AttendanceAuthenticationDefaults.MembershipIdClaim, employee.MembershipId.ToString()),
            new(AttendanceAuthenticationDefaults.EmployeeProfileIdClaim, employee.EmployeeProfileId.ToString())
        };

        if (employee.EmployeeNumber is not null)
        {
            claims.Add(new Claim(AttendanceAuthenticationDefaults.EmployeeNumberClaim, employee.EmployeeNumber));
        }

        claims.AddRange(employee.Roles.Select(role => new Claim(ClaimTypes.Role, role)));

        return new ClaimsPrincipal(new ClaimsIdentity(claims, AttendanceAuthenticationDefaults.SchemeName));
    }
}
