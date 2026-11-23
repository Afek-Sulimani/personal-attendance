namespace Personal.Attendance.Application.Attendance;

public sealed class AdminAttendanceAccessDeniedException(string message) : Exception(message);
public sealed class AdminAttendanceNotFoundException(string message) : Exception(message);
public sealed class AdminAttendanceConflictException(string message) : Exception(message);
public sealed class AdminAttendanceUnavailableException(string message, Exception? innerException = null)
    : Exception(message, innerException);
