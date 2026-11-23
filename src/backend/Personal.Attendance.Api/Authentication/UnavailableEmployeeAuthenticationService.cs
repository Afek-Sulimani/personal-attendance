using Personal.Attendance.Application.Authentication;

namespace Personal.Attendance.Api.Authentication;

public sealed class UnavailableEmployeeAuthenticationService : IEmployeeAuthenticationService
{
    public Task<AuthenticatedEmployee?> AuthenticateAsync(
        EmployeeLoginCredentials credentials,
        CancellationToken cancellationToken) =>
        throw new AuthenticationUnavailableException(
            "Configure the Attendance SQL Server connection before using authentication.");
}
