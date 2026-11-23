using Personal.Attendance.Domain.Enums;

namespace Personal.Attendance.Application.Profiles;

public sealed record EmployeeProfileDetails(
    string Email,
    string UserName,
    IReadOnlyList<CompanyEmploymentDetails> Companies);

public sealed record CompanyEmploymentDetails(
    Guid MembershipId,
    string CompanyName,
    string CompanyCode,
    string DisplayName,
    string? EmployeeNumber,
    IReadOnlyList<string> Roles,
    EmployeeCompensationType? CompensationType,
    decimal? HourlyRate);

public sealed record ManagedEmployeeProfile(
    Guid MembershipId,
    string DisplayName,
    string Email,
    string? EmployeeNumber,
    EmployeeCompensationType? CompensationType,
    decimal? HourlyRate);

public sealed record CompanyEmployeeActor(Guid UserId, Guid CompanyId, Guid MembershipId);

public enum PasswordChangeStatus
{
    Succeeded,
    UserNotFound,
    CurrentPasswordInvalid,
    NewPasswordInvalid
}

public sealed record PasswordChangeResult(
    PasswordChangeStatus Status,
    IReadOnlyList<string> Errors);
