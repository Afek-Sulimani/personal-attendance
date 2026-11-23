namespace Personal.Attendance.Application.Time;

public interface ICurrentAttendanceClock
{
    Task<ClockReading> GetCurrentTimeAsync(string timeZoneId, CancellationToken cancellationToken);

    Task<ClockReading> GetCurrentTimeForAttendanceAsync(string timeZoneId, CancellationToken cancellationToken);
}
