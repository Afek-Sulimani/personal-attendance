namespace Personal.Attendance.Application.Attendance;

public sealed record MonthlyAttendanceReportShift(
    Guid WorkShiftId,
    DateOnly WorkDate,
    DateTimeOffset ClockInAtLocal,
    string ClockInTimeZoneId,
    DateTimeOffset? ClockOutAtLocal,
    string? ClockOutTimeZoneId,
    int? WorkedMinutes,
    bool IsOpen,
    DateTimeOffset WorkedSegmentStartAtLocal,
    DateTimeOffset? WorkedSegmentEndAtLocal,
    bool IsContinuation);

public sealed record MonthlyAttendanceReportDay(
    DateOnly Date,
    bool IsWorkday,
    string Status,
    IReadOnlyList<MonthlyAttendanceReportShift> Shifts,
    MonthlyAttendancePendingCorrection? PendingCorrection);

public sealed record MonthlyAttendancePendingCorrection(
    DateTimeOffset? ClockInAtLocal,
    DateTimeOffset? ClockOutAtLocal,
    string TimeZoneId,
    int? WorkedMinutes);

public sealed record MonthlyAttendanceReportView(
    Guid EmployeeMembershipId,
    string EmployeeName,
    string? EmployeeNumber,
    DateOnly PeriodStart,
    DateOnly PeriodEnd,
    string TimeZoneId,
    bool IsScheduleConfigured,
    int ScheduledWorkdayCount,
    int CompletedShiftCount,
    int OpenShiftCount,
    int MissingWorkdayCount,
    int PendingCorrectionCount,
    int TotalWorkedMinutes,
    int PendingCorrectionMinutes,
    IReadOnlyList<MonthlyAttendanceReportDay> Days);

public sealed record MonthlyAttendanceTeamMember(
    Guid EmployeeMembershipId,
    string EmployeeName,
    string? EmployeeNumber);

public interface IMonthlyAttendanceReportService
{
    Task<MonthlyAttendanceReportView> GetCurrentMineAsync(AttendanceActor actor, CancellationToken cancellationToken);

    Task<MonthlyAttendanceReportView> GetMineAsync(
        AttendanceActor actor,
        int year,
        int month,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<MonthlyAttendanceTeamMember>> GetTeamMembersAsync(
        AttendanceActor actor,
        CancellationToken cancellationToken);

    Task<MonthlyAttendanceReportView> GetCurrentTeamMemberAsync(
        AttendanceActor actor,
        Guid employeeMembershipId,
        CancellationToken cancellationToken);

    Task<MonthlyAttendanceReportView> GetTeamMemberAsync(
        AttendanceActor actor,
        Guid employeeMembershipId,
        int year,
        int month,
        CancellationToken cancellationToken);

}

public sealed class MonthlyAttendanceReportAccessDeniedException(string message) : Exception(message);
public sealed class MonthlyAttendanceReportConflictException(string message) : Exception(message);
public sealed class MonthlyAttendanceReportNotFoundException(string message) : Exception(message);
public sealed class MonthlyAttendanceReportUnavailableException(string message, Exception? inner = null) : Exception(message, inner);
