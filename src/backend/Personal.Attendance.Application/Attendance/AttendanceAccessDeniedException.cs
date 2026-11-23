namespace Personal.Attendance.Application.Attendance;

public sealed class AttendanceAccessDeniedException(string message) : Exception(message);
