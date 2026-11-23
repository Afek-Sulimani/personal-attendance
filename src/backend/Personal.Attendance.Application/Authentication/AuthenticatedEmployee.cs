namespace Personal.Attendance.Application.Authentication;

public sealed record AuthenticatedEmployee(
    Guid UserId,
    string Email,
    string DisplayName,
    Guid CompanyId,
    string CompanyCode,
    Guid MembershipId,
    Guid EmployeeProfileId,
    string? EmployeeNumber,
    IReadOnlyList<string> Roles);
