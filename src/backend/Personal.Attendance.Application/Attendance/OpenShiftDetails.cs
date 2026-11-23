namespace Personal.Attendance.Application.Attendance;

public sealed record OpenShiftDetails(
    Guid WorkShiftId,
    DateTimeOffset ClockInAtUtc,
    string ClockInTimeZoneId,
    DateTimeOffset ClockInAtLocal,
    string? ClockInProvider,
    DateTimeOffset? ClockInProviderSampleUtc);
