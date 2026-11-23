using System.Data.Common;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Personal.Attendance.Application.Authentication;
using Personal.Attendance.Domain.Enums;
using Personal.Attendance.Infrastructure.Persistence;

namespace Personal.Attendance.Infrastructure.Authentication;

public sealed class EmployeeAuthenticationService(
    PersonalAttendanceDbContext dbContext,
    UserManager<PersonalUser> userManager) : IEmployeeAuthenticationService
{
    public async Task<AuthenticatedEmployee?> AuthenticateAsync(
        EmployeeLoginCredentials credentials,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(credentials);

        if (string.IsNullOrWhiteSpace(credentials.CompanyIdentifier) ||
            string.IsNullOrWhiteSpace(credentials.Email) ||
            string.IsNullOrWhiteSpace(credentials.Password))
        {
            return null;
        }

        try
        {
            var normalizedIdentifier = credentials.CompanyIdentifier.Trim().ToUpperInvariant();
            var matchingCompanies = await dbContext.Companies
                .AsNoTracking()
                .Where(item => item.NormalizedCompanyCode == normalizedIdentifier && item.IsActive)
                .Select(item => new { item.Id, item.CompanyCode })
                .Take(2)
                .ToListAsync(cancellationToken);

            if (matchingCompanies.Count == 0)
            {
                matchingCompanies = await dbContext.Companies
                    .AsNoTracking()
                    .Where(item => item.Name.ToUpper() == normalizedIdentifier && item.IsActive)
                    .Select(item => new { item.Id, item.CompanyCode })
                    .Take(2)
                    .ToListAsync(cancellationToken);
            }

            // Company names are not unique; require the generated ID when a name is ambiguous.
            if (matchingCompanies.Count != 1)
            {
                return null;
            }

            var company = matchingCompanies[0];

            var user = await userManager.FindByEmailAsync(credentials.Email.Trim());
            if (user is null || !user.EmailConfirmed ||
                !await userManager.CheckPasswordAsync(user, credentials.Password))
            {
                return null;
            }

            var membership = await dbContext.CompanyMemberships
                .AsNoTracking()
                .Include(item => item.Roles)
                .Include(item => item.EmployeeProfile)
                .SingleOrDefaultAsync(
                    item => item.CompanyId == company.Id &&
                            item.UserId == user.Id &&
                            item.IsActive,
                    cancellationToken);

            if (membership?.EmployeeProfile is not { } employeeProfile)
            {
                return null;
            }

            return new AuthenticatedEmployee(
                user.Id,
                user.Email!,
                employeeProfile.DisplayName,
                company.Id,
                company.CompanyCode,
                membership.MembershipId,
                employeeProfile.EmployeeProfileId,
                employeeProfile.EmployeeNumber,
                membership.Roles.Select(role => FormatRole(role.Role)).ToArray());
        }
        catch (DbException exception)
        {
            throw new AuthenticationUnavailableException(
                "The authentication data store is unavailable.",
                exception);
        }
    }

    private static string FormatRole(CompanyRole role) => role switch
    {
        CompanyRole.Admin => "Admin",
        CompanyRole.Hr => "HR",
        CompanyRole.PayrollRepresentative => "PayrollRepresentative",
        _ => role.ToString()
    };
}
