namespace Personal.Attendance.Application.Attendance;

public sealed class AttendanceCorrectionConflictException(string message) : Exception(message);
