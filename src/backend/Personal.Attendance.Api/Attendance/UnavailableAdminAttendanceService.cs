using Personal.Attendance.Application.Attendance;

namespace Personal.Attendance.Api.Attendance;

public sealed class UnavailableAdminAttendanceService : IAdminAttendanceService
{
    public Task<AdminAttendanceMonth> GetMonthAsync(
        AdminAttendanceActor actor,
        Guid employeeMembershipId,
        int year,
        int month,
        CancellationToken cancellationToken) =>
        Task.FromException<AdminAttendanceMonth>(
            new AdminAttendanceUnavailableException("Attendance storage is not configured."));

    public Task<AdminAttendanceChange> UpdateShiftAsync(
        AdminAttendanceActor actor,
        Guid workShiftId,
        DateTime clockInLocalDateTime,
        DateTime? clockOutLocalDateTime,
        string reason,
        CancellationToken cancellationToken) =>
        Task.FromException<AdminAttendanceChange>(
            new AdminAttendanceUnavailableException("Attendance storage is not configured."));
}
