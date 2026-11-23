namespace Personal.Attendance.Application.Attendance;

public sealed record SubmitAttendanceCorrectionCommand(
    AttendanceActor Actor,
    DateOnly WorkDate,
    TimeOnly? ClockInLocalTime,
    TimeOnly? ClockOutLocalTime,
    bool ClockOutNextDay);
