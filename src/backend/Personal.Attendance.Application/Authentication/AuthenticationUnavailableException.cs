namespace Personal.Attendance.Application.Authentication;

public sealed class AuthenticationUnavailableException(string message, Exception? innerException = null)
    : Exception(message, innerException);
