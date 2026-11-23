namespace Personal.Attendance.Application.Companies;

public interface ICompanyRegistrationService
{
    Task<(CompanyRegistrationOperationStatus Status, CompanyRegistrationCreated? Registration)> RegisterAsync(
        CompanyRegistrationCommand command,
        CancellationToken cancellationToken);
}
