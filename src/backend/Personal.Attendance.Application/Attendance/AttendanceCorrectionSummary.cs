namespace Personal.Attendance.Application.Attendance;

public sealed record AttendanceCorrectionSummary(
    Guid RequestId,
    string EmployeeName,
    string? EmployeeNumber,
    DateOnly WorkDate,
    string TimeZoneId,
    DateTimeOffset? RequestedClockInAtLocal,
    DateTimeOffset? RequestedClockOutAtLocal,
    DateTimeOffset SubmittedAtLocal,
    string Status,
    string? RejectionReason,
    bool IsAttentionRequired);
