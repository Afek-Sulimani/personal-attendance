namespace Personal.Attendance.Application.Invitations;

public sealed record EmployeeInvitationCreated(
    Guid InvitationId,
    string ActivationUrl,
    DateTimeOffset ExpiresAtUtc);
