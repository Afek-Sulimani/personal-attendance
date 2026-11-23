namespace Personal.Attendance.Application.Attendance;

public sealed class AttendanceCorrectionNotFoundException(string message) : Exception(message);
