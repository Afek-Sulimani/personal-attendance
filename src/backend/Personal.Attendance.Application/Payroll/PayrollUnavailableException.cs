namespace Personal.Attendance.Application.Payroll;

public sealed class PayrollUnavailableException(string message, Exception? innerException = null)
    : Exception(message, innerException);
