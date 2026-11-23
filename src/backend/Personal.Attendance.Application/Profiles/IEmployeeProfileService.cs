using Personal.Attendance.Domain.Enums;

namespace Personal.Attendance.Application.Profiles;

public interface IEmployeeProfileService
{
    Task<EmployeeProfileDetails> GetMyProfileAsync(CompanyEmployeeActor actor, CancellationToken cancellationToken);

    Task<PasswordChangeResult> ChangePasswordAsync(
        CompanyEmployeeActor actor,
        string currentPassword,
        string newPassword,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<ManagedEmployeeProfile>> GetCompanyEmployeesAsync(
        CompanyEmployeeActor actor,
        CancellationToken cancellationToken);

    Task<ManagedEmployeeProfile> UpdateCompensationAsync(
        CompanyEmployeeActor actor,
        Guid employeeMembershipId,
        EmployeeCompensationType compensationType,
        decimal? hourlyRate,
        CancellationToken cancellationToken);
}
