namespace Personal.Attendance.Application.Time;

public sealed record CurrentTimeSample(
    string Provider,
    string TimeZone,
    DateTimeOffset LocalTime,
    DateTimeOffset UtcTime,
    string UtcOffset,
    bool IsDaylightSaving,
    double RequestDurationMilliseconds);
