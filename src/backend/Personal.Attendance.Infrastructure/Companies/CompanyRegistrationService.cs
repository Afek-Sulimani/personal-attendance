using System.Data.Common;
using System.Globalization;
using System.Security.Cryptography;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Options;
using Personal.Attendance.Application.Companies;
using Personal.Attendance.Domain.Entities;
using Personal.Attendance.Domain.Enums;
using Personal.Attendance.Infrastructure.Invitations;
using Personal.Attendance.Infrastructure.Persistence;

namespace Personal.Attendance.Infrastructure.Companies;

public sealed class CompanyRegistrationService(
    PersonalAttendanceDbContext dbContext,
    TimeProvider timeProvider,
    IOptions<InvitationLinkOptions> invitationOptions) : ICompanyRegistrationService
{
    private const int CompanyCodeGenerationAttempts = 5;

    public async Task<(CompanyRegistrationOperationStatus Status, CompanyRegistrationCreated? Registration)> RegisterAsync(
        CompanyRegistrationCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (string.IsNullOrWhiteSpace(command.CompanyName) || command.CompanyName.Trim().Length > 200 ||
            string.IsNullOrWhiteSpace(command.CompanyRepresentativeName) || command.CompanyRepresentativeName.Trim().Length > 200 ||
            string.IsNullOrWhiteSpace(command.CompanyRepresentativeEmail) || command.CompanyRepresentativeEmail.Trim().Length > 256)
        {
            return (CompanyRegistrationOperationStatus.Invalid, null);
        }

        var nowUtc = timeProvider.GetUtcNow();
        var token = InvitationToken.Create();

        for (var attempt = 1; attempt <= CompanyCodeGenerationAttempts; attempt++)
        {
            IDbContextTransaction? transaction = null;
            try
            {
                var companyCode = CreateCompanyCode();
                var normalizedCompanyCode = companyCode.ToUpperInvariant();
                var codeAlreadyExists = await dbContext.Companies.AnyAsync(
                    company => company.NormalizedCompanyCode == normalizedCompanyCode,
                    cancellationToken);
                if (codeAlreadyExists)
                {
                    continue;
                }

                var company = Company.Create(companyCode, command.CompanyName, nowUtc);
                var invitation = EmployeeInvitation.Create(
                    company.Id,
                    null,
                    null,
                    command.CompanyRepresentativeEmail,
                    command.CompanyRepresentativeName,
                    null,
                    [CompanyRole.Admin],
                    InvitationToken.Hash(token),
                    nowUtc,
                    nowUtc.Add(InvitationLinkPolicy.Lifetime));

                if (dbContext.Database.IsRelational())
                {
                    transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
                }

                dbContext.Companies.Add(company);
                dbContext.EmployeeInvitations.Add(invitation);
                await dbContext.SaveChangesAsync(cancellationToken);

                if (transaction is not null)
                {
                    await transaction.CommitAsync(cancellationToken);
                }

                return (CompanyRegistrationOperationStatus.Succeeded, new CompanyRegistrationCreated(
                    company.Name,
                    company.CompanyCode,
                    invitation.DisplayName,
                    invitation.InvitedEmail,
                    InvitationToken.BuildActivationUrl(token, invitationOptions.Value.PublicBaseUrl)));
            }
            catch (DbUpdateException exception)
            {
                await RollBackAsync(transaction, cancellationToken);
                dbContext.ChangeTracker.Clear();

                if (IsUniqueConstraintViolation(exception))
                {
                    if (attempt < CompanyCodeGenerationAttempts)
                    {
                        continue;
                    }

                    return (CompanyRegistrationOperationStatus.Conflict, null);
                }

                return (ContainsDatabaseException(exception)
                    ? CompanyRegistrationOperationStatus.Unavailable
                    : CompanyRegistrationOperationStatus.Conflict, null);
            }
            catch (DbException)
            {
                await RollBackAsync(transaction, cancellationToken);
                dbContext.ChangeTracker.Clear();
                return (CompanyRegistrationOperationStatus.Unavailable, null);
            }
            finally
            {
                if (transaction is not null)
                {
                    await transaction.DisposeAsync();
                }
            }
        }

        return (CompanyRegistrationOperationStatus.Conflict, null);
    }

    private static string CreateCompanyCode() =>
        RandomNumberGenerator.GetInt32(10_000, 100_000).ToString(CultureInfo.InvariantCulture);

    private static bool IsUniqueConstraintViolation(Exception exception)
    {
        for (Exception? current = exception; current is not null; current = current.InnerException)
        {
            if (current is SqlException { Number: 2601 or 2627 })
            {
                return true;
            }
        }

        return false;
    }

    private static async Task RollBackAsync(
        IDbContextTransaction? transaction,
        CancellationToken cancellationToken)
    {
        if (transaction is not null)
        {
            await transaction.RollbackAsync(cancellationToken);
        }
    }

    private static bool ContainsDatabaseException(Exception exception)
    {
        for (Exception? current = exception; current is not null; current = current.InnerException)
        {
            if (current is DbException) return true;
        }

        return false;
    }
}
