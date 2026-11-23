namespace Personal.Attendance.Application.Invitations;

public sealed record EmployeeInvitationOperationResult(
    EmployeeInvitationOperationStatus Status,
    IReadOnlyList<string> Errors,
    string? CompanyCode = null)
{
    public static EmployeeInvitationOperationResult Success(string companyCode) =>
        new(EmployeeInvitationOperationStatus.Succeeded, [], companyCode);

    public static EmployeeInvitationOperationResult Failure(
        EmployeeInvitationOperationStatus status,
        params string[] errors) => new(status, errors);
}
