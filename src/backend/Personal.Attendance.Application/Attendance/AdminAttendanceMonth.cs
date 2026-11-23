namespace Personal.Attendance.Application.Attendance;

public sealed record AdminAttendanceMonth(
    Guid EmployeeMembershipId,
    int Year,
    int Month,
    IReadOnlyList<AdminAttendanceShift> Shifts,
    IReadOnlyList<AdminAttendanceChange> Changes);

public sealed record AdminAttendanceShift(
    Guid WorkShiftId,
    DateOnly WorkDate,
    string EmployeeName,
    string? EmployeeNumber,
    DateTimeOffset ClockInAtLocal,
    string ClockInTimeZoneId,
    string ClockInSource,
    DateTimeOffset? ClockOutAtLocal,
    string? ClockOutTimeZoneId,
    string? ClockOutSource);

public sealed record AdminAttendanceChange(
    Guid AuditId,
    Guid ChangedByMembershipId,
    string ChangedByName,
    DateOnly PreviousWorkDate,
    DateOnly WorkDate,
    string EmployeeName,
    string? EmployeeNumber,
    DateTimeOffset PreviousClockInAtLocal,
    string PreviousClockInTimeZoneId,
    string PreviousClockInSource,
    string? PreviousClockInProvider,
    DateTimeOffset? PreviousClockInProviderSampleUtc,
    DateTimeOffset ClockInAtLocal,
    string ClockInTimeZoneId,
    string ClockInSource,
    string? ClockInProvider,
    DateTimeOffset? ClockInProviderSampleUtc,
    DateTimeOffset? PreviousClockOutAtLocal,
    string? PreviousClockOutTimeZoneId,
    string? PreviousClockOutSource,
    string? PreviousClockOutProvider,
    DateTimeOffset? PreviousClockOutProviderSampleUtc,
    DateTimeOffset? ClockOutAtLocal,
    string? ClockOutTimeZoneId,
    string? ClockOutSource,
    string? ClockOutProvider,
    DateTimeOffset? ClockOutProviderSampleUtc,
    string Reason,
    DateTimeOffset ChangedAtLocal,
    string ChangeTimeZoneId,
    string ChangeTimeProvider,
    DateTimeOffset ChangeProviderSampleUtc);
