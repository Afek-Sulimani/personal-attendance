namespace Personal.Attendance.Application.Time;

public sealed record ClockReading(
    string TimeZone,
    string Provider,
    DateTimeOffset UtcTime,
    DateTimeOffset LocalTime,
    DateTimeOffset ProviderSampleUtcTime,
    double SampleAgeMilliseconds,
    double ProviderRequestDurationMilliseconds,
    double LatencyCompensationMilliseconds);
