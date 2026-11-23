namespace Personal.Attendance.Application.Companies;

public sealed record CompanyRegistrationCommand(
    string CompanyName,
    string CompanyRepresentativeName,
    string CompanyRepresentativeEmail);
