namespace Personal.Attendance.Application.Attendance;

public sealed class AttendanceCorrectionUnavailableException(string message, Exception? innerException = null)
    : Exception(message, innerException);
