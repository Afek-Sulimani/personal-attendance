using Personal.Attendance.Domain.Enums;

namespace Personal.Attendance.Domain.Entities;

public sealed record AttendancePunchSnapshot(
    DateOnly WorkDate,
    DateTimeOffset ClockInAtUtc,
    string ClockInTimeZoneId,
    AttendanceTimeSource ClockInSource,
    string? ClockInProvider,
    DateTimeOffset? ClockInProviderSampleUtc,
    DateTimeOffset? ClockOutAtUtc,
    string? ClockOutTimeZoneId,
    AttendanceTimeSource? ClockOutSource,
    string? ClockOutProvider,
    DateTimeOffset? ClockOutProviderSampleUtc)
{
    public static AttendancePunchSnapshot From(WorkShift shift)
    {
        ArgumentNullException.ThrowIfNull(shift);
        return new AttendancePunchSnapshot(
            shift.WorkDate,
            shift.ClockInAtUtc,
            shift.ClockInTimeZoneId,
            shift.ClockInSource,
            shift.ClockInProvider,
            shift.ClockInProviderSampleUtc,
            shift.ClockOutAtUtc,
            shift.ClockOutTimeZoneId,
            shift.ClockOutSource,
            shift.ClockOutProvider,
            shift.ClockOutProviderSampleUtc);
    }
}
