namespace Personal.Attendance.Application.Attendance;

public sealed record AttendanceActor(
    Guid UserId,
    Guid CompanyId,
    Guid MembershipId,
    Guid EmployeeProfileId);
