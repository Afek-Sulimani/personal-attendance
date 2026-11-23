namespace Personal.Attendance.Application.Payroll;

public sealed record PayrollActor(Guid UserId, Guid CompanyId, Guid MembershipId);
