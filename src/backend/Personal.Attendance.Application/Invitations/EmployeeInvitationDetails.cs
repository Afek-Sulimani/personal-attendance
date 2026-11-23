namespace Personal.Attendance.Application.Invitations;

public sealed record EmployeeInvitationDetails(
    string Email,
    string DisplayName,
    DateTimeOffset ExpiresAtUtc,
    string CompanyCode);
