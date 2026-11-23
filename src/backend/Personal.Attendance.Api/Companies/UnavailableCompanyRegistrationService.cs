using Personal.Attendance.Application.Companies;

namespace Personal.Attendance.Api.Companies;

public sealed class UnavailableCompanyRegistrationService : ICompanyRegistrationService
{
    public Task<(CompanyRegistrationOperationStatus Status, CompanyRegistrationCreated? Registration)> RegisterAsync(
        CompanyRegistrationCommand command,
        CancellationToken cancellationToken) =>
        Task.FromResult((CompanyRegistrationOperationStatus.Unavailable, (CompanyRegistrationCreated?)null));
}
