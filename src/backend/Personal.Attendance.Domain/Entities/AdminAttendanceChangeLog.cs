using Personal.Attendance.Domain.Enums;

namespace Personal.Attendance.Domain.Entities;

public sealed class AdminAttendanceChangeLog
{
    private AdminAttendanceChangeLog() { }

    public Guid AuditId { get; private set; }
    public Guid CompanyId { get; private set; }
    public Guid EmployeeProfileId { get; private set; }
    public Guid WorkShiftId { get; private set; }
    public Guid ChangedByMembershipId { get; private set; }
    public DateOnly PreviousWorkDate { get; private set; }
    public DateOnly WorkDate { get; private set; }

    public DateTimeOffset PreviousClockInAtUtc { get; private set; }
    public string PreviousClockInTimeZoneId { get; private set; } = string.Empty;
    public AttendanceTimeSource PreviousClockInSource { get; private set; }
    public string? PreviousClockInProvider { get; private set; }
    public DateTimeOffset? PreviousClockInProviderSampleUtc { get; private set; }
    public DateTimeOffset ClockInAtUtc { get; private set; }
    public string ClockInTimeZoneId { get; private set; } = string.Empty;
    public AttendanceTimeSource ClockInSource { get; private set; }
    public string? ClockInProvider { get; private set; }
    public DateTimeOffset? ClockInProviderSampleUtc { get; private set; }

    public DateTimeOffset? PreviousClockOutAtUtc { get; private set; }
    public string? PreviousClockOutTimeZoneId { get; private set; }
    public AttendanceTimeSource? PreviousClockOutSource { get; private set; }
    public string? PreviousClockOutProvider { get; private set; }
    public DateTimeOffset? PreviousClockOutProviderSampleUtc { get; private set; }
    public DateTimeOffset? ClockOutAtUtc { get; private set; }
    public string? ClockOutTimeZoneId { get; private set; }
    public AttendanceTimeSource? ClockOutSource { get; private set; }
    public string? ClockOutProvider { get; private set; }
    public DateTimeOffset? ClockOutProviderSampleUtc { get; private set; }

    public string Reason { get; private set; } = string.Empty;
    public DateTimeOffset ChangedAtUtc { get; private set; }
    public string ChangeTimeZoneId { get; private set; } = string.Empty;
    public string ChangeTimeProvider { get; private set; } = string.Empty;
    public DateTimeOffset ChangeProviderSampleUtc { get; private set; }

    public WorkShift WorkShift { get; private set; } = null!;

    public static AdminAttendanceChangeLog Create(
        WorkShift workShift,
        AttendancePunchSnapshot previous,
        AttendancePunchSnapshot corrected,
        Guid changedByMembershipId,
        string reason,
        DateTimeOffset changedAtUtc,
        string changeTimeZoneId,
        string changeTimeProvider,
        DateTimeOffset changeProviderSampleUtc)
    {
        ArgumentNullException.ThrowIfNull(workShift);
        ArgumentNullException.ThrowIfNull(previous);
        ArgumentNullException.ThrowIfNull(corrected);
        if (changedByMembershipId == Guid.Empty)
        {
            throw new ArgumentException("An Admin company membership is required.", nameof(changedByMembershipId));
        }
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        ArgumentException.ThrowIfNullOrWhiteSpace(changeTimeZoneId);
        ArgumentException.ThrowIfNullOrWhiteSpace(changeTimeProvider);
        if (reason.Trim().Length > 1000) throw new ArgumentOutOfRangeException(nameof(reason));
        EnsureUtc(changedAtUtc, nameof(changedAtUtc));
        EnsureUtc(changeProviderSampleUtc, nameof(changeProviderSampleUtc));

        return new AdminAttendanceChangeLog
        {
            AuditId = Guid.NewGuid(),
            CompanyId = workShift.CompanyId,
            EmployeeProfileId = workShift.EmployeeProfileId,
            WorkShiftId = workShift.WorkShiftId,
            ChangedByMembershipId = changedByMembershipId,
            PreviousWorkDate = previous.WorkDate,
            WorkDate = corrected.WorkDate,
            PreviousClockInAtUtc = previous.ClockInAtUtc,
            PreviousClockInTimeZoneId = previous.ClockInTimeZoneId,
            PreviousClockInSource = previous.ClockInSource,
            PreviousClockInProvider = previous.ClockInProvider,
            PreviousClockInProviderSampleUtc = previous.ClockInProviderSampleUtc,
            ClockInAtUtc = corrected.ClockInAtUtc,
            ClockInTimeZoneId = corrected.ClockInTimeZoneId,
            ClockInSource = corrected.ClockInSource,
            ClockInProvider = corrected.ClockInProvider,
            ClockInProviderSampleUtc = corrected.ClockInProviderSampleUtc,
            PreviousClockOutAtUtc = previous.ClockOutAtUtc,
            PreviousClockOutTimeZoneId = previous.ClockOutTimeZoneId,
            PreviousClockOutSource = previous.ClockOutSource,
            PreviousClockOutProvider = previous.ClockOutProvider,
            PreviousClockOutProviderSampleUtc = previous.ClockOutProviderSampleUtc,
            ClockOutAtUtc = corrected.ClockOutAtUtc,
            ClockOutTimeZoneId = corrected.ClockOutTimeZoneId,
            ClockOutSource = corrected.ClockOutSource,
            ClockOutProvider = corrected.ClockOutProvider,
            ClockOutProviderSampleUtc = corrected.ClockOutProviderSampleUtc,
            Reason = reason.Trim(),
            ChangedAtUtc = changedAtUtc,
            ChangeTimeZoneId = changeTimeZoneId.Trim(),
            ChangeTimeProvider = changeTimeProvider.Trim(),
            ChangeProviderSampleUtc = changeProviderSampleUtc
        };
    }

    private static void EnsureUtc(DateTimeOffset value, string parameterName)
    {
        if (value.Offset != TimeSpan.Zero)
        {
            throw new ArgumentException("The value must use UTC offset zero.", parameterName);
        }
    }
}
