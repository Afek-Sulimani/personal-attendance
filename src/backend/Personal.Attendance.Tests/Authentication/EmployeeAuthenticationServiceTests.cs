using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Personal.Attendance.Application.Authentication;
using Personal.Attendance.Domain.Entities;
using Personal.Attendance.Domain.Enums;
using Personal.Attendance.Infrastructure.Authentication;
using Personal.Attendance.Infrastructure.Persistence;

namespace Personal.Attendance.Tests.Authentication;

[TestClass]
public sealed class EmployeeAuthenticationServiceTests
{
    private static readonly DateTimeOffset CreatedAtUtc =
        DateTimeOffset.Parse("2030-01-02T08:00:00Z", System.Globalization.CultureInfo.InvariantCulture);

    [TestMethod]
    public async Task AuthenticateAsync_ReturnsTheActiveCompanyMembershipForValidCredentials()
    {
        await using var provider = CreateServiceProvider();
        var (company, user, profile) = await AddEmployeeAsync(provider, emailConfirmed: true);
        var service = CreateService(provider);

        var result = await service.AuthenticateAsync(
            new EmployeeLoginCredentials(" test-01 ", "test.employee@example.invalid", "Valid_password_123!"),
            CancellationToken.None);

        Assert.IsNotNull(result);
        Assert.AreEqual(user.Id, result.UserId);
        Assert.AreEqual(company.Id, result.CompanyId);
        Assert.AreEqual(profile.EmployeeProfileId, result.EmployeeProfileId);
        CollectionAssert.AreEqual(new[] { "Employee", "HR" }, result.Roles.Order().ToArray());
    }

    [TestMethod]
    public async Task AuthenticateAsync_ReturnsNoAccountForWrongPasswordOrCompanyCode()
    {
        await using var provider = CreateServiceProvider();
        await AddEmployeeAsync(provider, emailConfirmed: true);
        var service = CreateService(provider);

        var wrongPassword = await service.AuthenticateAsync(
            new EmployeeLoginCredentials("TEST-01", "test.employee@example.invalid", "Wrong_password_123!"),
            CancellationToken.None);
        var wrongCompany = await service.AuthenticateAsync(
            new EmployeeLoginCredentials("OTHER-CO", "test.employee@example.invalid", "Valid_password_123!"),
            CancellationToken.None);

        Assert.IsNull(wrongPassword);
        Assert.IsNull(wrongCompany);
    }

    [TestMethod]
    public async Task AuthenticateAsync_RejectsUnconfirmedEmail()
    {
        await using var provider = CreateServiceProvider();
        await AddEmployeeAsync(provider, emailConfirmed: false);
        var service = CreateService(provider);

        var result = await service.AuthenticateAsync(
            new EmployeeLoginCredentials("TEST-01", "test.employee@example.invalid", "Valid_password_123!"),
            CancellationToken.None);

        Assert.IsNull(result);
    }

    private static ServiceProvider CreateServiceProvider()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDbContext<PersonalAttendanceDbContext>(options =>
            options.UseInMemoryDatabase(Guid.NewGuid().ToString()));
        services.AddIdentityCore<PersonalUser>(options => options.User.RequireUniqueEmail = true)
            .AddRoles<PersonalRole>()
            .AddEntityFrameworkStores<PersonalAttendanceDbContext>();

        return services.BuildServiceProvider();
    }

    private static EmployeeAuthenticationService CreateService(IServiceProvider provider) =>
        new(
            provider.GetRequiredService<PersonalAttendanceDbContext>(),
            provider.GetRequiredService<UserManager<PersonalUser>>());

    private static async Task<(Company Company, PersonalUser User, EmployeeProfile Profile)> AddEmployeeAsync(
        IServiceProvider provider,
        bool emailConfirmed)
    {
        var dbContext = provider.GetRequiredService<PersonalAttendanceDbContext>();
        var userManager = provider.GetRequiredService<UserManager<PersonalUser>>();
        var company = Company.Create("TEST-01", "Test Company", CreatedAtUtc);
        var user = new PersonalUser
        {
            Id = Guid.NewGuid(),
            UserName = "test.employee@example.invalid",
            Email = "test.employee@example.invalid",
            EmailConfirmed = emailConfirmed
        };

        var createResult = await userManager.CreateAsync(user, "Valid_password_123!");
        Assert.IsTrue(createResult.Succeeded);

        var membership = CompanyMembership.Create(company.Id, user.Id, null, CreatedAtUtc);
        membership.AssignRole(CompanyRole.Employee);
        membership.AssignRole(CompanyRole.Hr);

        var profile = EmployeeProfile.Create(
            company.Id,
            membership.MembershipId,
            "Test Employee",
            "EMP-001",
            CreatedAtUtc);

        dbContext.AddRange(company, membership, profile);
        await dbContext.SaveChangesAsync();

        return (company, user, profile);
    }
}
