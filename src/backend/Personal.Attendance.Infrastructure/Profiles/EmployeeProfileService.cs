using System.Data.Common;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Personal.Attendance.Application.Profiles;
using Personal.Attendance.Domain.Entities;
using Personal.Attendance.Domain.Enums;
using Personal.Attendance.Infrastructure.Persistence;

namespace Personal.Attendance.Infrastructure.Profiles;

public sealed class EmployeeProfileService(
    PersonalAttendanceDbContext dbContext,
    UserManager<PersonalUser> userManager,
    TimeProvider timeProvider) : IEmployeeProfileService
{
    public async Task<EmployeeProfileDetails> GetMyProfileAsync(
        CompanyEmployeeActor actor,
        CancellationToken cancellationToken)
    {
        await GetActiveMembershipAsync(actor, cancellationToken);

        try
        {
            var user = await userManager.FindByIdAsync(actor.UserId.ToString());
            if (user is null)
            {
                throw new EmployeeProfileNotFoundException("The account was not found.");
            }

            var memberships = await dbContext.CompanyMemberships
                .AsNoTracking()
                .Include(membership => membership.Company)
                .Include(membership => membership.EmployeeProfile)
                .Include(membership => membership.Roles)
                .Where(membership => membership.UserId == actor.UserId &&
                                     membership.IsActive &&
                                     membership.Company.IsActive &&
                                     membership.EmployeeProfile != null)
                .OrderBy(membership => membership.Company.Name)
                .ToArrayAsync(cancellationToken);

            return new EmployeeProfileDetails(
                user.Email ?? string.Empty,
                user.UserName ?? string.Empty,
                memberships.Select(membership => new CompanyEmploymentDetails(
                    membership.MembershipId,
                    membership.Company.Name,
                    membership.Company.CompanyCode,
                    membership.EmployeeProfile!.DisplayName,
                    membership.EmployeeProfile.EmployeeNumber,
                    membership.Roles.Select(role => FormatRole(role.Role)).Order().ToArray(),
                    membership.EmployeeProfile.CompensationType,
                    membership.EmployeeProfile.HourlyRate)).ToArray());
        }
        catch (EmployeeProfileNotFoundException)
        {
            throw;
        }
        catch (DbException exception)
        {
            throw Unavailable("Profile information could not be loaded.", exception);
        }
    }

    public async Task<PasswordChangeResult> ChangePasswordAsync(
        CompanyEmployeeActor actor,
        string currentPassword,
        string newPassword,
        CancellationToken cancellationToken)
    {
        await GetActiveMembershipAsync(actor, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentException.ThrowIfNullOrWhiteSpace(currentPassword);
        ArgumentException.ThrowIfNullOrWhiteSpace(newPassword);

        try
        {
            var user = await userManager.FindByIdAsync(actor.UserId.ToString());
            if (user is null)
            {
                return new PasswordChangeResult(PasswordChangeStatus.UserNotFound, []);
            }

            var result = await userManager.ChangePasswordAsync(user, currentPassword, newPassword);
            if (result.Succeeded)
            {
                return new PasswordChangeResult(PasswordChangeStatus.Succeeded, []);
            }

            var errors = result.Errors.ToArray();
            return errors.Any(error => error.Code == "PasswordMismatch")
                ? new PasswordChangeResult(PasswordChangeStatus.CurrentPasswordInvalid, [])
                : new PasswordChangeResult(
                    PasswordChangeStatus.NewPasswordInvalid,
                    errors.Select(error => error.Description).ToArray());
        }
        catch (DbException exception)
        {
            throw Unavailable("The password could not be changed.", exception);
        }
    }

    public async Task<IReadOnlyList<ManagedEmployeeProfile>> GetCompanyEmployeesAsync(
        CompanyEmployeeActor actor,
        CancellationToken cancellationToken)
    {
        await EnsureManagerAccessAsync(actor, cancellationToken);

        try
        {
            var employees = await (
                from membership in dbContext.CompanyMemberships.AsNoTracking()
                join profile in dbContext.EmployeeProfiles.AsNoTracking()
                    on new { membership.CompanyId, membership.MembershipId }
                    equals new { profile.CompanyId, profile.MembershipId }
                join user in dbContext.Users.AsNoTracking() on membership.UserId equals user.Id
                where membership.CompanyId == actor.CompanyId &&
                      membership.IsActive &&
                      membership.Company.IsActive
                orderby profile.DisplayName, profile.EmployeeNumber
                select new ManagedEmployeeProfile(
                    membership.MembershipId,
                    profile.DisplayName,
                    user.Email ?? string.Empty,
                    profile.EmployeeNumber,
                    profile.CompensationType,
                    profile.HourlyRate))
                .ToArrayAsync(cancellationToken);

            return employees;
        }
        catch (DbException exception)
        {
            throw Unavailable("The company employee profiles could not be loaded.", exception);
        }
    }

    public async Task<ManagedEmployeeProfile> UpdateCompensationAsync(
        CompanyEmployeeActor actor,
        Guid employeeMembershipId,
        EmployeeCompensationType compensationType,
        decimal? hourlyRate,
        CancellationToken cancellationToken)
    {
        await EnsureManagerAccessAsync(actor, cancellationToken);
        if (employeeMembershipId == Guid.Empty)
        {
            throw new EmployeeProfileNotFoundException("The employee profile was not found in this company.");
        }

        try
        {
            var profile = await dbContext.EmployeeProfiles
                .Include(item => item.Membership)
                .SingleOrDefaultAsync(item => item.CompanyId == actor.CompanyId &&
                                             item.MembershipId == employeeMembershipId &&
                                             item.Membership.IsActive,
                    cancellationToken);
            if (profile is null)
            {
                throw new EmployeeProfileNotFoundException("The employee profile was not found in this company.");
            }

            var userEmail = await dbContext.Users
                .Where(user => user.Id == profile.Membership.UserId)
                .Select(user => user.Email)
                .SingleOrDefaultAsync(cancellationToken);

            profile.UpdateCompensation(compensationType, hourlyRate, timeProvider.GetUtcNow().ToUniversalTime());
            await dbContext.SaveChangesAsync(cancellationToken);

            return new ManagedEmployeeProfile(
                profile.MembershipId,
                profile.DisplayName,
                userEmail ?? string.Empty,
                profile.EmployeeNumber,
                profile.CompensationType,
                profile.HourlyRate);
        }
        catch (EmployeeProfileNotFoundException)
        {
            throw;
        }
        catch (ArgumentException)
        {
            throw;
        }
        catch (DbUpdateException exception)
        {
            throw Unavailable("The employee compensation could not be saved.", exception);
        }
        catch (DbException exception)
        {
            throw Unavailable("The employee compensation could not be saved.", exception);
        }
    }

    private async Task EnsureManagerAccessAsync(
        CompanyEmployeeActor actor,
        CancellationToken cancellationToken)
    {
        var membership = await GetActiveMembershipAsync(actor, cancellationToken);
        if (!membership.Roles.Any(role => role.Role is CompanyRole.Admin or CompanyRole.Hr or CompanyRole.PayrollRepresentative))
        {
            throw new EmployeeProfileAccessDeniedException("An Admin, HR, or Payroll role is required.");
        }
    }

    private async Task<CompanyMembership> GetActiveMembershipAsync(
        CompanyEmployeeActor actor,
        CancellationToken cancellationToken)
    {
        if (actor.UserId == Guid.Empty || actor.CompanyId == Guid.Empty || actor.MembershipId == Guid.Empty)
        {
            throw new EmployeeProfileAccessDeniedException("The session does not identify an active company membership.");
        }

        try
        {
            var membership = await dbContext.CompanyMemberships
                .AsNoTracking()
                .Include(item => item.Roles)
                .SingleOrDefaultAsync(membership => membership.MembershipId == actor.MembershipId &&
                                                   membership.CompanyId == actor.CompanyId &&
                                                   membership.UserId == actor.UserId &&
                                                   membership.IsActive &&
                                                   membership.Company.IsActive,
                    cancellationToken);

            return membership ?? throw new EmployeeProfileAccessDeniedException(
                "An active company membership is required.");
        }
        catch (DbException exception)
        {
            throw Unavailable("The active company membership could not be verified.", exception);
        }
    }

    private static string FormatRole(CompanyRole role) => role switch
    {
        CompanyRole.Hr => "HR",
        CompanyRole.PayrollRepresentative => "Payroll",
        CompanyRole.TeamLead => "Team lead",
        _ => role.ToString()
    };

    private static EmployeeProfileUnavailableException Unavailable(string message, Exception exception) =>
        new(message, exception);
}
