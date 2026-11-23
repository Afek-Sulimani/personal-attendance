namespace Personal.Attendance.Application.Attendance;

public sealed record AttendanceCalendarDay(
    DateOnly Date,
    bool? IsWorkDay,
    AttendanceDayStatus Status,
    string? ClockInTimeZoneId,
    DateTimeOffset? ClockInAtLocal,
    string? ClockOutTimeZoneId,
    DateTimeOffset? ClockOutAtLocal,
    string? CorrectionStatus);
