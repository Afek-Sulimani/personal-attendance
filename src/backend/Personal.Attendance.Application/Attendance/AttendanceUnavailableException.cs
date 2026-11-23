namespace Personal.Attendance.Application.Attendance;

public sealed class AttendanceUnavailableException(string message, Exception? innerException = null)
    : Exception(message, innerException);
