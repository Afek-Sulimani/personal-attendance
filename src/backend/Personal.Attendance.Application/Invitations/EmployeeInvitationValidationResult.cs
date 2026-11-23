namespace Personal.Attendance.Application.Invitations;

public sealed record EmployeeInvitationValidationResult(
    EmployeeInvitationOperationStatus Status,
    EmployeeInvitationDetails? Details = null);
