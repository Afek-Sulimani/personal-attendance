using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Personal.Attendance.Application.Invitations;
using Personal.Attendance.Domain.Entities;
using Personal.Attendance.Domain.Enums;
using Personal.Attendance.Infrastructure.Invitations;
using Personal.Attendance.Infrastructure.Persistence;

namespace Personal.Attendance.Tests.Invitations;

[TestClass]
public sealed class EmployeeInvitationServiceTests
{
    private static readonly DateTimeOffset NowUtc =
        DateTimeOffset.Parse("2030-02-03T10:00:00Z", System.Globalization.CultureInfo.InvariantCulture);

    [TestMethod]
    public async Task CreateAsync_ReturnsSingleUseLinkAndStoresOnlyItsHashFor48Hours()
    {
        await using var fixture = await InvitationFixture.CreateAsync(CompanyRole.Hr);

        var (status, invitation) = await fixture.Service.CreateAsync(
            fixture.CreateCommand("worker@example.invalid", ["Employee"]),
            CancellationToken.None);

        Assert.AreEqual(EmployeeInvitationOperationStatus.Succeeded, status);
        Assert.IsNotNull(invitation);
        Assert.IsTrue(invitation.ActivationUrl.StartsWith("http://localhost:5173/activate#token=", StringComparison.Ordinal));
        var token = ReadToken(invitation.ActivationUrl);
        var storedInvitation = await fixture.DbContext.EmployeeInvitations.SingleAsync();
        Assert.AreNotEqual(token, storedInvitation.TokenHash);
        Assert.AreEqual(
            Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token))),
            storedInvitation.TokenHash);
        Assert.AreEqual(TimeSpan.FromHours(48), storedInvitation.ExpiresAtUtc - NowUtc);
    }

    [TestMethod]
    public async Task ValidateAsync_StopsAcceptingTheLinkAtIts48HourExpiry()
    {
        await using var fixture = await InvitationFixture.CreateAsync(CompanyRole.Hr);
        var invitation = await fixture.CreateInvitationAsync("worker@example.invalid");
        var token = ReadToken(invitation.ActivationUrl);

        fixture.Clock.Advance(TimeSpan.FromHours(48) - TimeSpan.FromSeconds(1));
        var justBeforeExpiry = await fixture.Service.ValidateAsync(token, CancellationToken.None);
        fixture.Clock.Advance(TimeSpan.FromSeconds(1));
        var atExpiry = await fixture.Service.ValidateAsync(token, CancellationToken.None);

        Assert.AreEqual(EmployeeInvitationOperationStatus.Succeeded, justBeforeExpiry.Status);
        Assert.AreEqual(EmployeeInvitationOperationStatus.InvalidOrExpired, atExpiry.Status);
    }

    [TestMethod]
    public async Task AcceptAsync_CreatesAndConfirmsNewAccountThenRejectsTokenReuse()
    {
        await using var fixture = await InvitationFixture.CreateAsync(CompanyRole.Hr);
        var invitation = await fixture.CreateInvitationAsync("worker@example.invalid", "Worker One", ["Employee"]);
        var token = ReadToken(invitation.ActivationUrl);

        var accepted = await fixture.Service.AcceptAsync(
            token,
            "Strong_password_123!",
            authenticatedUserId: null,
            CancellationToken.None);
        var reused = await fixture.Service.AcceptAsync(
            token,
            "Strong_password_123!",
            authenticatedUserId: null,
            CancellationToken.None);

        Assert.AreEqual(EmployeeInvitationOperationStatus.Succeeded, accepted.Status);
        Assert.AreEqual("TEST-01", accepted.CompanyCode);
        Assert.AreEqual(EmployeeInvitationOperationStatus.InvalidOrExpired, reused.Status);

        var user = await fixture.UserManager.FindByEmailAsync("worker@example.invalid");
        Assert.IsNotNull(user);
        Assert.IsTrue(user.EmailConfirmed);
        Assert.IsTrue(await fixture.UserManager.CheckPasswordAsync(user, "Strong_password_123!"));
        var membership = await fixture.DbContext.CompanyMemberships
            .Include(item => item.Roles)
            .Include(item => item.EmployeeProfile)
            .SingleAsync(item => item.CompanyId == fixture.Company.Id && item.UserId == user.Id);
        Assert.AreEqual("Worker One", membership.EmployeeProfile!.DisplayName);
        Assert.AreEqual("00001", membership.EmployeeProfile.EmployeeNumber);
        CollectionAssert.AreEqual(new[] { CompanyRole.Employee }, membership.Roles.Select(role => role.Role).ToArray());
    }

    [TestMethod]
    public async Task AcceptAsync_AssignsSequentialNumbersAndDoesNotAddEmployeeToPayroll()
    {
        await using var fixture = await InvitationFixture.CreateAsync(CompanyRole.Hr);
        var first = await fixture.CreateInvitationAsync("first@example.invalid", roles: ["Employee", "PayrollRepresentative"]);
        var second = await fixture.CreateInvitationAsync("second@example.invalid", roles: ["Employee"]);

        var firstResult = await fixture.Service.AcceptAsync(ReadToken(first.ActivationUrl), "Strong_password_123!", null, CancellationToken.None);
        var secondResult = await fixture.Service.AcceptAsync(ReadToken(second.ActivationUrl), "Strong_password_123!", null, CancellationToken.None);

        Assert.AreEqual(EmployeeInvitationOperationStatus.Succeeded, firstResult.Status);
        Assert.AreEqual(EmployeeInvitationOperationStatus.Succeeded, secondResult.Status);
        var profiles = await fixture.DbContext.EmployeeProfiles
            .Where(profile => profile.CompanyId == fixture.Company.Id && profile.EmployeeNumber != null)
            .OrderBy(profile => profile.EmployeeNumber)
            .ToArrayAsync();
        CollectionAssert.AreEqual(new[] { "00001", "00002" }, profiles.Select(profile => profile.EmployeeNumber).ToArray());
        var payrollMembership = await fixture.DbContext.CompanyMemberships.Include(item => item.Roles)
            .SingleAsync(item => item.MembershipId == profiles[0].MembershipId);
        CollectionAssert.AreEqual(new[] { CompanyRole.PayrollRepresentative }, payrollMembership.Roles.Select(role => role.Role).ToArray());
    }

    [TestMethod]
    public async Task AcceptAsync_RequiresExistingGlobalAccountToSignInBeforeJoiningAnotherCompany()
    {
        await using var fixture = await InvitationFixture.CreateAsync(CompanyRole.Hr);
        var existingUser = await fixture.AddEmployeeInOtherCompanyAsync("worker@example.invalid");
        var invitation = await fixture.CreateInvitationAsync("worker@example.invalid");
        var token = ReadToken(invitation.ActivationUrl);

        var anonymousAttempt = await fixture.Service.AcceptAsync(token, null, null, CancellationToken.None);
        var signedInAttempt = await fixture.Service.AcceptAsync(token, null, existingUser.Id, CancellationToken.None);

        Assert.AreEqual(EmployeeInvitationOperationStatus.ExistingAccountMustSignIn, anonymousAttempt.Status);
        Assert.AreEqual(EmployeeInvitationOperationStatus.Succeeded, signedInAttempt.Status);
        Assert.AreEqual(2, await fixture.DbContext.CompanyMemberships.CountAsync(item => item.UserId == existingUser.Id));
    }

    [TestMethod]
    public async Task AcceptAsync_UsesInvitationToSetPasswordAndConfirmAnExistingUnconfirmedAccount()
    {
        await using var fixture = await InvitationFixture.CreateAsync(CompanyRole.Hr);
        var existingUser = await fixture.AddEmployeeInOtherCompanyAsync("worker@example.invalid", emailConfirmed: false);
        var invitation = await fixture.CreateInvitationAsync("worker@example.invalid");

        var accepted = await fixture.Service.AcceptAsync(
            ReadToken(invitation.ActivationUrl),
            "Updated_password_123!",
            authenticatedUserId: null,
            CancellationToken.None);

        Assert.AreEqual(EmployeeInvitationOperationStatus.Succeeded, accepted.Status);
        Assert.IsTrue(existingUser.EmailConfirmed);
        Assert.IsTrue(await fixture.UserManager.CheckPasswordAsync(existingUser, "Updated_password_123!"));
        Assert.AreEqual(2, await fixture.DbContext.CompanyMemberships.CountAsync(item => item.UserId == existingUser.Id));
    }

    [TestMethod]
    public async Task CreateAsync_PayrollCanAssignNonAdminRolesButCannotAssignAdmin()
    {
        await using var fixture = await InvitationFixture.CreateAsync(CompanyRole.PayrollRepresentative);

        var employeeInvitation = await fixture.Service.CreateAsync(
            fixture.CreateCommand("worker@example.invalid", ["Employee"]),
            CancellationToken.None);
        var leadInvitation = await fixture.Service.CreateAsync(
            fixture.CreateCommand("lead@example.invalid", ["Employee", "TeamLead"]),
            CancellationToken.None);
        var hrInvitation = await fixture.Service.CreateAsync(
            fixture.CreateCommand("hr@example.invalid", ["HR"]),
            CancellationToken.None);
        var payrollInvitation = await fixture.Service.CreateAsync(
            fixture.CreateCommand("payroll@example.invalid", ["PayrollRepresentative"]),
            CancellationToken.None);
        var adminInvitation = await fixture.Service.CreateAsync(
            fixture.CreateCommand("admin2@example.invalid", ["Admin"]),
            CancellationToken.None);

        Assert.AreEqual(EmployeeInvitationOperationStatus.Succeeded, employeeInvitation.Status);
        Assert.AreEqual(EmployeeInvitationOperationStatus.Succeeded, leadInvitation.Status);
        Assert.AreEqual(EmployeeInvitationOperationStatus.Succeeded, hrInvitation.Status);
        Assert.AreEqual(EmployeeInvitationOperationStatus.Succeeded, payrollInvitation.Status);
        Assert.AreEqual(EmployeeInvitationOperationStatus.Forbidden, adminInvitation.Status);
        Assert.AreEqual(4, await fixture.DbContext.EmployeeInvitations.CountAsync());
    }

    [TestMethod]
    public async Task CreateAsync_RejectsAnActiveEmployeeWithoutManagerRoleAsTheDirectManager()
    {
        await using var fixture = await InvitationFixture.CreateAsync(CompanyRole.Hr);
        var employeeMembershipId = await fixture.AddEmployeeInCurrentCompanyAsync("worker@example.invalid");
        var command = fixture.CreateCommand("new.worker@example.invalid", ["Employee"]) with
        {
            ManagerMembershipId = employeeMembershipId
        };

        var result = await fixture.Service.CreateAsync(command, CancellationToken.None);

        Assert.AreEqual(EmployeeInvitationOperationStatus.Conflict, result.Status);
        Assert.AreEqual(0, await fixture.DbContext.EmployeeInvitations.CountAsync());
    }

    [TestMethod]
    public async Task CreateAsync_ReissueRevokesThePreviousPendingLink()
    {
        await using var fixture = await InvitationFixture.CreateAsync(CompanyRole.Hr);
        var previous = await fixture.CreateInvitationAsync("worker@example.invalid");
        var current = await fixture.CreateInvitationAsync("worker@example.invalid");

        var previousValidation = await fixture.Service.ValidateAsync(ReadToken(previous.ActivationUrl), CancellationToken.None);
        var currentValidation = await fixture.Service.ValidateAsync(ReadToken(current.ActivationUrl), CancellationToken.None);

        Assert.AreEqual(EmployeeInvitationOperationStatus.InvalidOrExpired, previousValidation.Status);
        Assert.AreEqual(EmployeeInvitationOperationStatus.Succeeded, currentValidation.Status);
    }

    private static string ReadToken(string activationUrl)
    {
        var fragment = new Uri(activationUrl).Fragment;
        Assert.IsTrue(fragment.StartsWith("#token=", StringComparison.Ordinal));
        return Uri.UnescapeDataString(fragment["#token=".Length..]);
    }

    private sealed class InvitationFixture : IAsyncDisposable
    {
        private readonly ServiceProvider provider;

        private InvitationFixture(
            ServiceProvider provider,
            PersonalAttendanceDbContext dbContext,
            UserManager<PersonalUser> userManager,
            EmployeeInvitationService service,
            FakeTimeProvider clock,
            Company company,
            CompanyMembership creatorMembership)
        {
            this.provider = provider;
            DbContext = dbContext;
            UserManager = userManager;
            Service = service;
            Clock = clock;
            Company = company;
            CreatorMembership = creatorMembership;
        }

        public PersonalAttendanceDbContext DbContext { get; }
        public UserManager<PersonalUser> UserManager { get; }
        public EmployeeInvitationService Service { get; }
        public FakeTimeProvider Clock { get; }
        public Company Company { get; }
        public CompanyMembership CreatorMembership { get; }

        public static async Task<InvitationFixture> CreateAsync(CompanyRole creatorRole)
        {
            var clock = new FakeTimeProvider(NowUtc);
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddDbContext<PersonalAttendanceDbContext>(options =>
                options.UseInMemoryDatabase(Guid.NewGuid().ToString()));
            services.AddIdentityCore<PersonalUser>(options => options.User.RequireUniqueEmail = true)
                .AddRoles<PersonalRole>()
                .AddEntityFrameworkStores<PersonalAttendanceDbContext>()
                .AddDefaultTokenProviders();
            services.AddDataProtection();
            var provider = services.BuildServiceProvider();
            var dbContext = provider.GetRequiredService<PersonalAttendanceDbContext>();
            var userManager = provider.GetRequiredService<UserManager<PersonalUser>>();
            var company = Company.Create("TEST-01", "Test Company", NowUtc);
            var creator = new PersonalUser
            {
                UserName = "admin@example.invalid",
                Email = "admin@example.invalid",
                EmailConfirmed = true
            };
            var createUser = await userManager.CreateAsync(creator, "Strong_password_123!");
            Assert.IsTrue(createUser.Succeeded);

            var membership = CompanyMembership.Create(company.Id, creator.Id, null, NowUtc);
            membership.AssignRole(creatorRole);
            if (creatorRole == CompanyRole.PayrollRepresentative)
            {
                membership.AssignRole(CompanyRole.TeamLead);
            }
            var profile = EmployeeProfile.Create(company.Id, membership.MembershipId, "Admin User", null, NowUtc);
            dbContext.AddRange(company, membership, profile);
            await dbContext.SaveChangesAsync();

            var service = new EmployeeInvitationService(
                dbContext,
                userManager,
                clock,
                Microsoft.Extensions.Options.Options.Create(new InvitationLinkOptions
                {
                    PublicBaseUrl = "http://localhost:5173"
                }));

            return new InvitationFixture(provider, dbContext, userManager, service, clock, company, membership);
        }

        public CreateEmployeeInvitationCommand CreateCommand(string email, string[] roles) => new(
            Company.Id,
            CreatorMembership.MembershipId,
            email,
            "New Worker",
            CreatorMembership.MembershipId,
            roles);

        public async Task<EmployeeInvitationCreated> CreateInvitationAsync(
            string email,
            string displayName = "New Worker",
            string[]? roles = null)
        {
            var result = await Service.CreateAsync(
                new CreateEmployeeInvitationCommand(
                    Company.Id,
                    CreatorMembership.MembershipId,
                    email,
                    displayName,
                    CreatorMembership.MembershipId,
                    roles ?? ["Employee"]),
                CancellationToken.None);

            Assert.AreEqual(EmployeeInvitationOperationStatus.Succeeded, result.Status);
            Assert.IsNotNull(result.Invitation);
            return result.Invitation;
        }

        public async Task<PersonalUser> AddEmployeeInOtherCompanyAsync(string email, bool emailConfirmed = true)
        {
            var user = new PersonalUser { UserName = email, Email = email, EmailConfirmed = emailConfirmed };
            var result = await UserManager.CreateAsync(user, "Strong_password_123!");
            Assert.IsTrue(result.Succeeded);
            var otherCompany = Company.Create("OTHER-01", "Other Company", NowUtc);
            var otherMembership = CompanyMembership.Create(otherCompany.Id, user.Id, null, NowUtc);
            otherMembership.AssignRole(CompanyRole.Employee);
            var profile = EmployeeProfile.Create(otherCompany.Id, otherMembership.MembershipId, "Existing Worker", null, NowUtc);
            DbContext.AddRange(otherCompany, otherMembership, profile);
            await DbContext.SaveChangesAsync();
            return user;
        }

        public async Task<Guid> AddEmployeeInCurrentCompanyAsync(string email)
        {
            var user = new PersonalUser { UserName = email, Email = email, EmailConfirmed = true };
            var result = await UserManager.CreateAsync(user, "Strong_password_123!");
            Assert.IsTrue(result.Succeeded);
            var membership = CompanyMembership.Create(Company.Id, user.Id, null, NowUtc);
            membership.AssignRole(CompanyRole.Employee);
            var profile = EmployeeProfile.Create(Company.Id, membership.MembershipId, "Regular Employee", null, NowUtc);
            DbContext.AddRange(membership, profile);
            await DbContext.SaveChangesAsync();
            return membership.MembershipId;
        }

        public async ValueTask DisposeAsync() => await provider.DisposeAsync();
    }
}
