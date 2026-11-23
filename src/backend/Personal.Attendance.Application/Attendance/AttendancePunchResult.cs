namespace Personal.Attendance.Application.Attendance;

public sealed record AttendancePunchResult(
    Guid WorkShiftId,
    string Status,
    DateTimeOffset OccurredAtUtc,
    DateTimeOffset OccurredAtLocal,
    string TimeZone,
    string Provider,
    DateTimeOffset ProviderSampleUtc);
