using Personal.Attendance.Application.Invitations;

namespace Personal.Attendance.Api.Invitations;

public sealed class UnavailableEmployeeInvitationService : IEmployeeInvitationService
{
    public Task<(EmployeeInvitationOperationStatus Status, IReadOnlyList<EmployeeInvitationManager> Managers)> GetManagersAsync(
        Guid companyId,
        Guid requestingMembershipId,
        CancellationToken cancellationToken) =>
        Task.FromResult((EmployeeInvitationOperationStatus.Unavailable, (IReadOnlyList<EmployeeInvitationManager>)[]));

    public Task<(EmployeeInvitationOperationStatus Status, EmployeeInvitationCreated? Invitation)> CreateAsync(
        CreateEmployeeInvitationCommand command,
        CancellationToken cancellationToken) =>
        Task.FromResult((EmployeeInvitationOperationStatus.Unavailable, (EmployeeInvitationCreated?)null));

    public Task<EmployeeInvitationValidationResult> ValidateAsync(
        string token,
        CancellationToken cancellationToken) =>
        Task.FromResult(new EmployeeInvitationValidationResult(EmployeeInvitationOperationStatus.Unavailable));

    public Task<EmployeeInvitationOperationResult> AcceptAsync(
        string token,
        string? password,
        Guid? authenticatedUserId,
        CancellationToken cancellationToken) =>
        Task.FromResult(EmployeeInvitationOperationResult.Failure(EmployeeInvitationOperationStatus.Unavailable));
}
