namespace Personal.Attendance.Application.Attendance;

public sealed record AdminAttendanceActor(Guid UserId, Guid CompanyId, Guid MembershipId);
