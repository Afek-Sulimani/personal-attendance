using Personal.Attendance.Application.Attendance;

namespace Personal.Attendance.Api.Attendance;

public sealed class UnavailableAttendanceService : IAttendanceService
{
    public Task<AttendanceCalendar> GetCalendarAsync(
        AttendanceActor actor,
        int? year,
        int? month,
        CancellationToken cancellationToken) =>
        throw new AttendanceUnavailableException(
            "Configure the Attendance SQL Server connection before using attendance actions.");

    public Task<AttendanceState> GetCurrentAsync(
        AttendanceActor actor,
        CancellationToken cancellationToken) =>
        throw new AttendanceUnavailableException(
            "Configure the Attendance SQL Server connection before using attendance actions.");

    public Task<AttendancePunchResult> ClockInAsync(
        AttendanceActor actor,
        CancellationToken cancellationToken) =>
        throw new AttendanceUnavailableException(
            "Configure the Attendance SQL Server connection before using attendance actions.");

    public Task<AttendancePunchResult> ClockOutAsync(
        AttendanceActor actor,
        CancellationToken cancellationToken) =>
        throw new AttendanceUnavailableException(
            "Configure the Attendance SQL Server connection before using attendance actions.");
}
