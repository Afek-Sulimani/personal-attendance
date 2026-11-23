using System.Data.Common;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Options;
using Personal.Attendance.Application.Invitations;
using Personal.Attendance.Domain.Entities;
using Personal.Attendance.Domain.Enums;
using Personal.Attendance.Infrastructure.Persistence;

namespace Personal.Attendance.Infrastructure.Invitations;

public sealed class EmployeeInvitationService(
    PersonalAttendanceDbContext dbContext,
    UserManager<PersonalUser> userManager,
    TimeProvider timeProvider,
    IOptions<InvitationLinkOptions> options) : IEmployeeInvitationService
{
    public static readonly TimeSpan InvitationLifetime = InvitationLinkPolicy.Lifetime;

    public async Task<(EmployeeInvitationOperationStatus Status, IReadOnlyList<EmployeeInvitationManager> Managers)> GetManagersAsync(
        Guid companyId,
        Guid requestingMembershipId,
        CancellationToken cancellationToken)
    {
        if (companyId == Guid.Empty || requestingMembershipId == Guid.Empty)
        {
            return (EmployeeInvitationOperationStatus.Forbidden, []);
        }

        try
        {
            var requester = await dbContext.CompanyMemberships
                .Include(membership => membership.Roles)
                .SingleOrDefaultAsync(
                    membership => membership.CompanyId == companyId &&
                                   membership.MembershipId == requestingMembershipId &&
                                   membership.IsActive && membership.Company.IsActive,
                    cancellationToken);
            if (requester is null || !requester.Roles.Any(role =>
                    role.Role is CompanyRole.Admin or CompanyRole.Hr or CompanyRole.PayrollRepresentative))
            {
                return (EmployeeInvitationOperationStatus.Forbidden, []);
            }

            var managers = await dbContext.CompanyMemberships
                .AsNoTracking()
                .Where(membership => membership.CompanyId == companyId &&
                                     membership.IsActive &&
                                     membership.EmployeeProfile != null &&
                                     membership.Roles.Any(role => role.Role == CompanyRole.Admin ||
                                                                  role.Role == CompanyRole.TeamLead ||
                                                                  role.Role == CompanyRole.Hr))
                .OrderBy(membership => membership.EmployeeProfile!.DisplayName)
                .Select(membership => new EmployeeInvitationManager(
                    membership.MembershipId,
                    membership.EmployeeProfile!.DisplayName,
                    membership.EmployeeProfile.EmployeeNumber))
                .ToArrayAsync(cancellationToken);

            return (EmployeeInvitationOperationStatus.Succeeded, managers);
        }
        catch (DbException)
        {
            return (EmployeeInvitationOperationStatus.Unavailable, []);
        }
    }

    public async Task<(EmployeeInvitationOperationStatus Status, EmployeeInvitationCreated? Invitation)> CreateAsync(
        CreateEmployeeInvitationCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (command.CompanyId == Guid.Empty || command.CreatedByMembershipId == Guid.Empty ||
            string.IsNullOrWhiteSpace(command.Email) || string.IsNullOrWhiteSpace(command.DisplayName) ||
            command.Roles is null || command.Roles.Count == 0)
        {
            return (EmployeeInvitationOperationStatus.Conflict, null);
        }
        if (command.ManagerMembershipId is null)
        {
            return (EmployeeInvitationOperationStatus.ManagerRequired, null);
        }

        var nowUtc = timeProvider.GetUtcNow();
        try
        {
            var creator = await dbContext.CompanyMemberships
                .Include(membership => membership.Roles)
                .SingleOrDefaultAsync(
                    membership => membership.CompanyId == command.CompanyId &&
                                   membership.MembershipId == command.CreatedByMembershipId &&
                                   membership.IsActive && membership.Company.IsActive,
                    cancellationToken);

            if (creator is null)
            {
                return (EmployeeInvitationOperationStatus.Forbidden, null);
            }

            var creatorRoles = creator.Roles.Select(role => role.Role).ToHashSet();
            var canCreateInvitations = creatorRoles.Contains(CompanyRole.Admin) ||
                                       creatorRoles.Contains(CompanyRole.Hr) ||
                                       creatorRoles.Contains(CompanyRole.PayrollRepresentative);
            if (!canCreateInvitations || !TryParseRoles(command.Roles, out var roles))
            {
                return (EmployeeInvitationOperationStatus.Forbidden, null);
            }

            if (!creatorRoles.Contains(CompanyRole.Admin) && roles.Contains(CompanyRole.Admin))
            {
                return (EmployeeInvitationOperationStatus.Forbidden, null);
            }

            var email = command.Email.Trim();
            var normalizedEmail = userManager.NormalizeEmail(email);
            if (string.IsNullOrWhiteSpace(normalizedEmail))
            {
                return (EmployeeInvitationOperationStatus.Conflict, null);
            }

            var existingUser = await userManager.FindByEmailAsync(email);
            if (existingUser is not null && await dbContext.CompanyMemberships.AnyAsync(
                    membership => membership.CompanyId == command.CompanyId &&
                                  membership.UserId == existingUser.Id,
                    cancellationToken))
            {
                return (EmployeeInvitationOperationStatus.Conflict, null);
            }

            if (command.ManagerMembershipId is { } managerMembershipId &&
                !await dbContext.CompanyMemberships.AnyAsync(
                    membership => membership.CompanyId == command.CompanyId &&
                                  membership.MembershipId == managerMembershipId &&
                                  membership.IsActive &&
                                  membership.EmployeeProfile != null &&
                                  membership.Roles.Any(role => role.Role == CompanyRole.Admin ||
                                                               role.Role == CompanyRole.TeamLead ||
                                                               role.Role == CompanyRole.Hr),
                    cancellationToken))
            {
                return (EmployeeInvitationOperationStatus.Conflict, null);
            }

            var previousInvitations = await dbContext.EmployeeInvitations
                .Where(invitation => invitation.CompanyId == command.CompanyId &&
                                     invitation.NormalizedEmail == normalizedEmail &&
                                     invitation.AcceptedAtUtc == null &&
                                     invitation.RevokedAtUtc == null &&
                                     invitation.ExpiresAtUtc > nowUtc)
                .ToListAsync(cancellationToken);
            foreach (var previousInvitation in previousInvitations)
            {
                previousInvitation.Revoke(nowUtc);
            }

            var token = InvitationToken.Create();
            var invitation = EmployeeInvitation.Create(
                command.CompanyId,
                command.CreatedByMembershipId,
                command.ManagerMembershipId,
                email,
                command.DisplayName,
                null,
                roles,
                InvitationToken.Hash(token),
                nowUtc,
                nowUtc.Add(InvitationLifetime));

            dbContext.EmployeeInvitations.Add(invitation);
            await dbContext.SaveChangesAsync(cancellationToken);

            return (EmployeeInvitationOperationStatus.Succeeded, new EmployeeInvitationCreated(
                invitation.InvitationId,
                InvitationToken.BuildActivationUrl(token, options.Value.PublicBaseUrl),
                invitation.ExpiresAtUtc));
        }
        catch (DbUpdateException exception)
        {
            return (ContainsDatabaseException(exception)
                ? EmployeeInvitationOperationStatus.Unavailable
                : EmployeeInvitationOperationStatus.Conflict, null);
        }
        catch (DbException)
        {
            return (EmployeeInvitationOperationStatus.Unavailable, null);
        }
    }

    public async Task<EmployeeInvitationValidationResult> ValidateAsync(
        string token,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(token) || token.Length > 128)
        {
            return new EmployeeInvitationValidationResult(EmployeeInvitationOperationStatus.InvalidOrExpired);
        }

        try
        {
            var tokenHash = InvitationToken.Hash(token);
            var invitation = await dbContext.EmployeeInvitations
                .AsNoTracking()
                .SingleOrDefaultAsync(item => item.TokenHash == tokenHash, cancellationToken);
            if (invitation is null || !InvitationToken.Matches(invitation.TokenHash, token) ||
                !invitation.IsUsableAt(timeProvider.GetUtcNow()))
            {
                return new EmployeeInvitationValidationResult(EmployeeInvitationOperationStatus.InvalidOrExpired);
            }

            var companyCode = await dbContext.Companies
                .Where(company => company.Id == invitation.CompanyId && company.IsActive)
                .Select(company => company.CompanyCode)
                .SingleOrDefaultAsync(cancellationToken);
            if (companyCode is null)
            {
                return new EmployeeInvitationValidationResult(EmployeeInvitationOperationStatus.InvalidOrExpired);
            }

            return new EmployeeInvitationValidationResult(
                EmployeeInvitationOperationStatus.Succeeded,
                new EmployeeInvitationDetails(
                    invitation.InvitedEmail,
                    invitation.DisplayName,
                    invitation.ExpiresAtUtc,
                    companyCode));
        }
        catch (DbException)
        {
            return new EmployeeInvitationValidationResult(EmployeeInvitationOperationStatus.Unavailable);
        }
    }

    public async Task<EmployeeInvitationOperationResult> AcceptAsync(
        string token,
        string? password,
        Guid? authenticatedUserId,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(token) || token.Length > 128)
        {
            return EmployeeInvitationOperationResult.Failure(EmployeeInvitationOperationStatus.InvalidOrExpired);
        }

        var nowUtc = timeProvider.GetUtcNow();
        IDbContextTransaction? transaction = null;
        try
        {
            var tokenHash = InvitationToken.Hash(token);
            var invitation = await dbContext.EmployeeInvitations
                .SingleOrDefaultAsync(item => item.TokenHash == tokenHash, cancellationToken);
            if (invitation is null || !InvitationToken.Matches(invitation.TokenHash, token) ||
                !invitation.IsUsableAt(nowUtc))
            {
                return EmployeeInvitationOperationResult.Failure(EmployeeInvitationOperationStatus.InvalidOrExpired);
            }

            var company = await dbContext.Companies
                .SingleOrDefaultAsync(item => item.Id == invitation.CompanyId && item.IsActive, cancellationToken);
            if (company is null)
            {
                return EmployeeInvitationOperationResult.Failure(EmployeeInvitationOperationStatus.InvalidOrExpired);
            }

            var user = await userManager.FindByEmailAsync(invitation.InvitedEmail);
            if (authenticatedUserId is not null && user?.Id != authenticatedUserId)
            {
                return EmployeeInvitationOperationResult.Failure(EmployeeInvitationOperationStatus.InvalidOrExpired);
            }

            if (user is not null && user.EmailConfirmed && authenticatedUserId != user.Id)
            {
                return EmployeeInvitationOperationResult.Failure(
                    EmployeeInvitationOperationStatus.ExistingAccountMustSignIn);
            }

            if (user is null || !user.EmailConfirmed)
            {
                if (string.IsNullOrWhiteSpace(password))
                {
                    return EmployeeInvitationOperationResult.Failure(EmployeeInvitationOperationStatus.PasswordRequired);
                }
            }

            if (user is not null && await dbContext.CompanyMemberships.AnyAsync(
                    membership => membership.CompanyId == invitation.CompanyId &&
                                  membership.UserId == user.Id,
                    cancellationToken))
            {
                return EmployeeInvitationOperationResult.Failure(EmployeeInvitationOperationStatus.Conflict);
            }

            if (invitation.ManagerMembershipId is { } managerMembershipId &&
                !await dbContext.CompanyMemberships.AnyAsync(
                    membership => membership.CompanyId == invitation.CompanyId &&
                                  membership.MembershipId == managerMembershipId &&
                                  membership.IsActive &&
                                  membership.EmployeeProfile != null &&
                                  membership.Roles.Any(role => role.Role == CompanyRole.Admin ||
                                                               role.Role == CompanyRole.TeamLead ||
                                                               role.Role == CompanyRole.Hr),
                    cancellationToken))
            {
                return EmployeeInvitationOperationResult.Failure(EmployeeInvitationOperationStatus.Conflict);
            }

            if (dbContext.Database.IsRelational())
            {
                transaction = await dbContext.Database.BeginTransactionAsync(
                    System.Data.IsolationLevel.Serializable, cancellationToken);
            }

            // Lock the company row before allocating a number so concurrent activations cannot reuse it.
            if (dbContext.Database.IsSqlServer())
            {
                company = await dbContext.Companies.FromSqlInterpolated(
                    $"SELECT * FROM ATTENDANCE_COMPANIES WITH (UPDLOCK, HOLDLOCK) WHERE CompanyId = {invitation.CompanyId}")
                    .SingleAsync(cancellationToken);
            }

            if (user is null)
            {
                user = new PersonalUser
                {
                    UserName = invitation.InvitedEmail,
                    Email = invitation.InvitedEmail,
                    EmailConfirmed = true
                };

                var createResult = await userManager.CreateAsync(user, password!);
                if (!createResult.Succeeded)
                {
                    await RollBackAsync(transaction, cancellationToken);
                    return MapIdentityFailure(createResult);
                }
            }
            else if (!user.EmailConfirmed)
            {
                var resetToken = await userManager.GeneratePasswordResetTokenAsync(user);
                var resetResult = await userManager.ResetPasswordAsync(user, resetToken, password!);
                if (!resetResult.Succeeded)
                {
                    await RollBackAsync(transaction, cancellationToken);
                    return MapIdentityFailure(resetResult);
                }

                user.EmailConfirmed = true;
                var updateResult = await userManager.UpdateAsync(user);
                if (!updateResult.Succeeded)
                {
                    await RollBackAsync(transaction, cancellationToken);
                    return MapIdentityFailure(updateResult);
                }
            }

            var membership = CompanyMembership.Create(
                invitation.CompanyId,
                user.Id,
                invitation.ManagerMembershipId,
                nowUtc);
            foreach (var role in invitation.GetAssignedRoles())
            {
                membership.AssignRole(role);
            }

            var profile = EmployeeProfile.Create(
                invitation.CompanyId,
                membership.MembershipId,
                invitation.DisplayName,
                company.AllocateEmployeeNumber(),
                nowUtc);

            invitation.MarkAccepted(user.Id, nowUtc);
            dbContext.CompanyMemberships.Add(membership);
            dbContext.EmployeeProfiles.Add(profile);
            await dbContext.SaveChangesAsync(cancellationToken);
            if (transaction is not null)
            {
                await transaction.CommitAsync(cancellationToken);
            }

            return EmployeeInvitationOperationResult.Success(company.CompanyCode);
        }
        catch (DbUpdateConcurrencyException)
        {
            await RollBackAsync(transaction, cancellationToken);
            return EmployeeInvitationOperationResult.Failure(EmployeeInvitationOperationStatus.InvalidOrExpired);
        }
        catch (DbUpdateException exception)
        {
            await RollBackAsync(transaction, cancellationToken);
            return EmployeeInvitationOperationResult.Failure(
                ContainsDatabaseException(exception)
                    ? EmployeeInvitationOperationStatus.Unavailable
                    : EmployeeInvitationOperationStatus.Conflict);
        }
        catch (DbException)
        {
            await RollBackAsync(transaction, cancellationToken);
            return EmployeeInvitationOperationResult.Failure(EmployeeInvitationOperationStatus.Unavailable);
        }
        finally
        {
            if (transaction is not null)
            {
                await transaction.DisposeAsync();
            }
        }
    }

    private static bool TryParseRoles(IEnumerable<string> roleNames, out CompanyRole[] roles)
    {
        var parsed = new List<CompanyRole>();
        foreach (var roleName in roleNames)
        {
            if (string.IsNullOrWhiteSpace(roleName) || !TryParseRoleName(roleName, out var role))
            {
                roles = [];
                return false;
            }

            parsed.Add(role);
        }

        roles = parsed.Distinct().ToArray();
        if (roles.Length > 1)
        {
            roles = roles.Where(role => role != CompanyRole.Employee).ToArray();
        }
        return roles.Length > 0;
    }

    private static bool TryParseRoleName(string roleName, out CompanyRole role)
    {
        var parsedRole = roleName.Trim().ToUpperInvariant() switch
        {
            "ADMIN" => CompanyRole.Admin,
            "EMPLOYEE" => (CompanyRole?)CompanyRole.Employee,
            "TEAMLEAD" => CompanyRole.TeamLead,
            "HR" => CompanyRole.Hr,
            "PAYROLLREPRESENTATIVE" => CompanyRole.PayrollRepresentative,
            _ => null
        };

        if (parsedRole is null)
        {
            role = default;
            return false;
        }

        role = parsedRole.Value;
        return true;
    }

    private static EmployeeInvitationOperationResult MapIdentityFailure(IdentityResult result)
    {
        var errors = result.Errors.Select(error => error.Description).ToArray();
        var isPasswordFailure = result.Errors.Any(error => error.Code.StartsWith("Password", StringComparison.Ordinal));
        return EmployeeInvitationOperationResult.Failure(
            isPasswordFailure ? EmployeeInvitationOperationStatus.InvalidPassword : EmployeeInvitationOperationStatus.Conflict,
            errors);
    }

    private static async Task RollBackAsync(IDbContextTransaction? transaction, CancellationToken cancellationToken)
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
            if (current is DbException)
            {
                return true;
            }
        }

        return false;
    }
}
