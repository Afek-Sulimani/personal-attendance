namespace Personal.Attendance.Application.Profiles;

public sealed class EmployeeProfileAccessDeniedException(string message) : Exception(message);

public sealed class EmployeeProfileNotFoundException(string message) : Exception(message);

public sealed class EmployeeProfileUnavailableException(string message, Exception? innerException = null)
    : Exception(message, innerException);
