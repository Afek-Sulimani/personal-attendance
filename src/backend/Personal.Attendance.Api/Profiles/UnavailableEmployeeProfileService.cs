using Personal.Attendance.Application.Profiles;
using Personal.Attendance.Domain.Enums;

namespace Personal.Attendance.Api.Profiles;

public sealed class UnavailableEmployeeProfileService : IEmployeeProfileService
{
    public Task<EmployeeProfileDetails> GetMyProfileAsync(CompanyEmployeeActor actor, CancellationToken cancellationToken) =>
        Task.FromException<EmployeeProfileDetails>(Unavailable());

    public Task<PasswordChangeResult> ChangePasswordAsync(
        CompanyEmployeeActor actor,
        string currentPassword,
        string newPassword,
        CancellationToken cancellationToken) =>
        Task.FromException<PasswordChangeResult>(Unavailable());

    public Task<IReadOnlyList<ManagedEmployeeProfile>> GetCompanyEmployeesAsync(
        CompanyEmployeeActor actor,
        CancellationToken cancellationToken) =>
        Task.FromException<IReadOnlyList<ManagedEmployeeProfile>>(Unavailable());

    public Task<ManagedEmployeeProfile> UpdateCompensationAsync(
        CompanyEmployeeActor actor,
        Guid employeeMembershipId,
        EmployeeCompensationType compensationType,
        decimal? hourlyRate,
        CancellationToken cancellationToken) =>
        Task.FromException<ManagedEmployeeProfile>(Unavailable());

    private static EmployeeProfileUnavailableException Unavailable() =>
        new("Profile services are unavailable because SQL Server is not configured.");
}
