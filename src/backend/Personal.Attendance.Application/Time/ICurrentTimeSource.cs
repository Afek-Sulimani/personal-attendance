namespace Personal.Attendance.Application.Time;

public interface ICurrentTimeSource
{
    string Id { get; }

    Task<CurrentTimeSample> GetCurrentTimeAsync(string timeZoneId, CancellationToken cancellationToken);
}
