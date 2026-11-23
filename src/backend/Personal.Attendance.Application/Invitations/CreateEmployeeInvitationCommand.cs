namespace Personal.Attendance.Application.Invitations;

public sealed record CreateEmployeeInvitationCommand(
    Guid CompanyId,
    Guid CreatedByMembershipId,
    string Email,
    string DisplayName,
    Guid? ManagerMembershipId,
    IReadOnlyCollection<string> Roles);
