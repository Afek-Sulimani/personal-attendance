namespace Personal.Attendance.Application.Attendance;

public interface IAttendanceService
{
    Task<AttendanceCalendar> GetCalendarAsync(
        AttendanceActor actor,
        int? year,
        int? month,
        CancellationToken cancellationToken);

    Task<AttendanceState> GetCurrentAsync(
        AttendanceActor actor,
        CancellationToken cancellationToken);

    Task<AttendancePunchResult> ClockInAsync(
        AttendanceActor actor,
        CancellationToken cancellationToken);

    Task<AttendancePunchResult> ClockOutAsync(
        AttendanceActor actor,
        CancellationToken cancellationToken);
}
