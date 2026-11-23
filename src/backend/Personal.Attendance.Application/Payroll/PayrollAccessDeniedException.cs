namespace Personal.Attendance.Application.Payroll;

public sealed class PayrollAccessDeniedException(string message) : Exception(message);
