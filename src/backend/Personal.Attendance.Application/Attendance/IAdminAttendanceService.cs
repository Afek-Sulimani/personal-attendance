namespace Personal.Attendance.Application.Attendance;

public interface IAdminAttendanceService
{
    Task<AdminAttendanceMonth> GetMonthAsync(
        AdminAttendanceActor actor,
        Guid employeeMembershipId,
        int year,
        int month,
        CancellationToken cancellationToken);

    Task<AdminAttendanceChange> UpdateShiftAsync(
        AdminAttendanceActor actor,
        Guid workShiftId,
        DateTime clockInLocalDateTime,
        DateTime? clockOutLocalDateTime,
        string reason,
        CancellationToken cancellationToken);
}
