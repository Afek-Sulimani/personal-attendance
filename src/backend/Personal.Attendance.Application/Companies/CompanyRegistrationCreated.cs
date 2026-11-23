namespace Personal.Attendance.Application.Companies;

public sealed record CompanyRegistrationCreated(
    string CompanyName,
    string CompanyCode,
    string CompanyRepresentativeName,
    string CompanyRepresentativeEmail,
    string ActivationUrl);
