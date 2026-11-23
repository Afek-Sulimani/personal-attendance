using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Personal.Attendance.Application.Profiles;
using Personal.Attendance.Domain.Entities;
using Personal.Attendance.Domain.Enums;
using Personal.Attendance.Infrastructure.Persistence;
using Personal.Attendance.Infrastructure.Profiles;

namespace Personal.Attendance.Tests.Profiles;

[TestClass]
public sealed class EmployeeProfileServiceTests
{
    private static readonly DateTimeOffset NowUtc = DateTimeOffset.Parse("2026-11-20T08:00:00Z");

    [TestMethod]
    public async Task GetMyProfileAsync_ReturnsAccountAndAllActiveCompanyMemberships()
    {
        await using var fixture = await ProfileFixture.CreateAsync();
        var otherCompany = Company.Create("TEST-02", "Second Company", NowUtc);
        var otherMembership = CompanyMembership.Create(otherCompany.Id, fixture.EmployeeUser.Id, null, NowUtc);
        otherMembership.AssignRole(CompanyRole.Employee);
        var otherProfile = EmployeeProfile.Create(otherCompany.Id, otherMembership.MembershipId, "Employee Name", "00003", NowUtc);
        fixture.DbContext.AddRange(otherCompany, otherMembership, otherProfile);
        await fixture.DbContext.SaveChangesAsync();

        var profile = await fixture.Service.GetMyProfileAsync(fixture.EmployeeActor, CancellationToken.None);

        Assert.AreEqual(fixture.EmployeeUser.Email, profile.Email);
        Assert.AreEqual(fixture.EmployeeUser.UserName, profile.UserName);
        Assert.AreEqual(2, profile.Companies.Count);
        Assert.IsTrue(profile.Companies.Any(company => company.CompanyCode == "TEST-01" && company.EmployeeNumber == "00002"));
        Assert.IsTrue(profile.Companies.Any(company => company.CompanyCode == "TEST-02" && company.EmployeeNumber == "00003"));
    }

    [TestMethod]
    public async Task UpdateCompensationAsync_HrCanSaveHourlyRateAndGlobalPayHasNoRate()
    {
        await using var fixture = await ProfileFixture.CreateAsync(managerRole: CompanyRole.Hr);

        var hourly = await fixture.Service.UpdateCompensationAsync(
            fixture.ManagerActor,
            fixture.EmployeeMembership.MembershipId,
            EmployeeCompensationType.Hourly,
            31.25m,
            CancellationToken.None);

        Assert.AreEqual(EmployeeCompensationType.Hourly.ToString(), hourly.CompensationType?.ToString());
        Assert.AreEqual(31.25m, hourly.HourlyRate);

        var global = await fixture.Service.UpdateCompensationAsync(
            fixture.ManagerActor,
            fixture.EmployeeMembership.MembershipId,
            EmployeeCompensationType.Global,
            null,
            CancellationToken.None);

        Assert.AreEqual(EmployeeCompensationType.Global.ToString(), global.CompensationType?.ToString());
        Assert.IsNull(global.HourlyRate);
    }

    [TestMethod]
    public async Task UpdateCompensationAsync_RejectsEmployeeRoleAndCrossCompanyTarget()
    {
        await using var fixture = await ProfileFixture.CreateAsync();

        await AssertThrowsAsync<EmployeeProfileAccessDeniedException>(() =>
            fixture.Service.UpdateCompensationAsync(
                fixture.EmployeeActor,
                fixture.ManagerMembership.MembershipId,
                EmployeeCompensationType.Global,
                null,
                CancellationToken.None));

        var otherCompany = Company.Create("TEST-02", "Second Company", NowUtc);
        var otherUser = await fixture.CreateUserAsync("other@example.invalid");
        var otherMembership = CompanyMembership.Create(otherCompany.Id, otherUser.Id, null, NowUtc);
        otherMembership.AssignRole(CompanyRole.Employee);
        var otherProfile = EmployeeProfile.Create(otherCompany.Id, otherMembership.MembershipId, "Other Employee", "00001", NowUtc);
        fixture.DbContext.AddRange(otherCompany, otherMembership, otherProfile);
        await fixture.DbContext.SaveChangesAsync();

        await AssertThrowsAsync<EmployeeProfileNotFoundException>(() =>
            fixture.Service.UpdateCompensationAsync(
                fixture.ManagerActor,
                otherMembership.MembershipId,
                EmployeeCompensationType.Global,
                null,
                CancellationToken.None));
    }

    [TestMethod]
    public async Task ChangePasswordAsync_UsesIdentityPolicyAndValidatesCurrentPassword()
    {
        await using var fixture = await ProfileFixture.CreateAsync();

        var wrongCurrentPassword = await fixture.Service.ChangePasswordAsync(
            fixture.EmployeeActor,
            "incorrect",
            "NewStrong_password_123!",
            CancellationToken.None);
        var weakNewPassword = await fixture.Service.ChangePasswordAsync(
            fixture.EmployeeActor,
            "Strong_password_123!",
            "weak",
            CancellationToken.None);

        Assert.AreEqual(PasswordChangeStatus.CurrentPasswordInvalid, wrongCurrentPassword.Status);
        Assert.AreEqual(PasswordChangeStatus.NewPasswordInvalid, weakNewPassword.Status);
        Assert.IsTrue(weakNewPassword.Errors.Count > 0);
    }

    private static async Task AssertThrowsAsync<TException>(Func<Task> action) where TException : Exception
    {
        try
        {
            await action();
        }
        catch (TException)
        {
            return;
        }

        Assert.Fail($"Expected {typeof(TException).Name} to be thrown.");
    }

    private sealed class ProfileFixture : IAsyncDisposable
    {
        private readonly ServiceProvider serviceProvider;

        private ProfileFixture(
            ServiceProvider serviceProvider,
            PersonalAttendanceDbContext dbContext,
            UserManager<PersonalUser> userManager,
            Company company,
            CompanyMembership managerMembership,
            CompanyMembership employeeMembership,
            PersonalUser employeeUser)
        {
            this.serviceProvider = serviceProvider;
            DbContext = dbContext;
            EmployeeUser = employeeUser;
            Company = company;
            ManagerMembership = managerMembership;
            EmployeeMembership = employeeMembership;
            UserManager = userManager;
            Service = new EmployeeProfileService(dbContext, userManager, new FakeTimeProvider(NowUtc.AddHours(1)));
            ManagerActor = new CompanyEmployeeActor(managerMembership.UserId, company.Id, managerMembership.MembershipId);
            EmployeeActor = new CompanyEmployeeActor(employeeUser.Id, company.Id, employeeMembership.MembershipId);
        }

        public PersonalAttendanceDbContext DbContext { get; }
        public UserManager<PersonalUser> UserManager { get; }
        public EmployeeProfileService Service { get; }
        public Company Company { get; }
        public CompanyMembership ManagerMembership { get; }
        public CompanyMembership EmployeeMembership { get; }
        public PersonalUser EmployeeUser { get; }
        public CompanyEmployeeActor ManagerActor { get; }
        public CompanyEmployeeActor EmployeeActor { get; }

        public async Task<PersonalUser> CreateUserAsync(string email)
        {
            var user = new PersonalUser { UserName = email, Email = email, EmailConfirmed = true };
            var result = await UserManager.CreateAsync(user, "Strong_password_123!");
            Assert.IsTrue(result.Succeeded, string.Join(" ", result.Errors.Select(error => error.Description)));
            return user;
        }

        public static async Task<ProfileFixture> CreateAsync(CompanyRole managerRole = CompanyRole.Admin)
        {
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddDataProtection();
            services.AddDbContext<PersonalAttendanceDbContext>(options =>
                options.UseInMemoryDatabase(Guid.NewGuid().ToString()));
            services.AddIdentityCore<PersonalUser>(options =>
                options.User.RequireUniqueEmail = true)
                .AddRoles<PersonalRole>()
                .AddEntityFrameworkStores<PersonalAttendanceDbContext>()
                .AddDefaultTokenProviders();

            var provider = services.BuildServiceProvider();
            var dbContext = provider.GetRequiredService<PersonalAttendanceDbContext>();
            var userManager = provider.GetRequiredService<UserManager<PersonalUser>>();
            var managerUser = new PersonalUser
            {
                UserName = "manager@example.invalid",
                Email = "manager@example.invalid",
                EmailConfirmed = true
            };
            var employeeUser = new PersonalUser
            {
                UserName = "employee@example.invalid",
                Email = "employee@example.invalid",
                EmailConfirmed = true
            };
            Assert.IsTrue((await userManager.CreateAsync(managerUser, "Strong_password_123!")).Succeeded);
            Assert.IsTrue((await userManager.CreateAsync(employeeUser, "Strong_password_123!")).Succeeded);

            var company = Company.Create("TEST-01", "Test Company", NowUtc);
            var managerMembership = CompanyMembership.Create(company.Id, managerUser.Id, null, NowUtc);
            managerMembership.AssignRole(managerRole);
            var managerProfile = EmployeeProfile.Create(company.Id, managerMembership.MembershipId, "Manager", "00001", NowUtc);
            var employeeMembership = CompanyMembership.Create(company.Id, employeeUser.Id, managerMembership.MembershipId, NowUtc);
            employeeMembership.AssignRole(CompanyRole.Employee);
            var employeeProfile = EmployeeProfile.Create(company.Id, employeeMembership.MembershipId, "Employee Name", "00002", NowUtc);
            dbContext.AddRange(company, managerMembership, managerProfile, employeeMembership, employeeProfile);
            await dbContext.SaveChangesAsync();

            return new ProfileFixture(provider, dbContext, userManager, company, managerMembership,
                employeeMembership, employeeUser);
        }

        public async ValueTask DisposeAsync() => await serviceProvider.DisposeAsync();
    }
}
