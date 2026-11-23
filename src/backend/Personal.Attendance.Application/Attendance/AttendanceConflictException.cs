namespace Personal.Attendance.Application.Attendance;

public sealed class AttendanceConflictException(string message) : Exception(message);
