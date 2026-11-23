namespace Personal.Attendance.Application.Attendance;

public sealed class AttendanceCorrectionAccessDeniedException(string message) : Exception(message);
