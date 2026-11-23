namespace Personal.Attendance.Application.Invitations;

public interface IEmployeeInvitationService
{
    Task<(EmployeeInvitationOperationStatus Status, IReadOnlyList<EmployeeInvitationManager> Managers)> GetManagersAsync(
        Guid companyId,
        Guid requestingMembershipId,
        CancellationToken cancellationToken);

    Task<(EmployeeInvitationOperationStatus Status, EmployeeInvitationCreated? Invitation)> CreateAsync(
        CreateEmployeeInvitationCommand command,
        CancellationToken cancellationToken);

    Task<EmployeeInvitationValidationResult> ValidateAsync(
        string token,
        CancellationToken cancellationToken);

    Task<EmployeeInvitationOperationResult> AcceptAsync(
        string token,
        string? password,
        Guid? authenticatedUserId,
        CancellationToken cancellationToken);
}
