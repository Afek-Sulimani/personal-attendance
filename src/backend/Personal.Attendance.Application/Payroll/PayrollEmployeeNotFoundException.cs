namespace Personal.Attendance.Application.Payroll;

public sealed class PayrollEmployeeNotFoundException(string message) : Exception(message);
