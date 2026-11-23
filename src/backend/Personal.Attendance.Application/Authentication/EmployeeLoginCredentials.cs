namespace Personal.Attendance.Application.Authentication;

public sealed record EmployeeLoginCredentials(
    string CompanyIdentifier,
    string Email,
    string Password);
