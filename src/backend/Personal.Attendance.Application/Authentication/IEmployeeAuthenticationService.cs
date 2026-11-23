namespace Personal.Attendance.Application.Authentication;

public interface IEmployeeAuthenticationService
{
    Task<AuthenticatedEmployee?> AuthenticateAsync(
        EmployeeLoginCredentials credentials,
        CancellationToken cancellationToken);
}
