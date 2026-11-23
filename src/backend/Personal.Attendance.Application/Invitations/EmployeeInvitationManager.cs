namespace Personal.Attendance.Application.Invitations;

public sealed record EmployeeInvitationManager(Guid MembershipId, string DisplayName, string? EmployeeNumber);
