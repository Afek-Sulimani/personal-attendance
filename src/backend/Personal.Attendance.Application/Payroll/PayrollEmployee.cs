namespace Personal.Attendance.Application.Payroll;

public sealed record PayrollEmployee(
    Guid MembershipId,
    string DisplayName,
    string? EmployeeNumber);
