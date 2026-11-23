namespace Personal.Attendance.Application.Attendance;

public sealed record AttendanceState(
    bool HasOpenShift,
    OpenShiftDetails? OpenShift);
