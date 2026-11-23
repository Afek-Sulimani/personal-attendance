using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Personal.Attendance.Api.Authentication;

namespace Personal.Attendance.Tests.Authentication;

[TestClass]
public sealed class AuthenticationRegistrationTests
{
    [TestMethod]
    public void AddPersonalAuthentication_ConfiguresProductionCookieAndTicketStore()
    {
        var services = new ServiceCollection();
        services.AddPersonalAuthentication(TimeSpan.FromHours(12), isDevelopment: false);
        using var provider = services.BuildServiceProvider();

        var cookie = provider.GetRequiredService<IOptionsMonitor<CookieAuthenticationOptions>>()
            .Get(AttendanceAuthenticationDefaults.SchemeName);
        var antiforgery = provider.GetRequiredService<IOptions<AntiforgeryOptions>>().Value;

        Assert.AreEqual("__Host-PersonalSession", cookie.Cookie.Name);
        Assert.IsTrue(cookie.Cookie.HttpOnly);
        Assert.AreEqual(CookieSecurePolicy.Always, cookie.Cookie.SecurePolicy);
        Assert.AreEqual(SameSiteMode.Lax, cookie.Cookie.SameSite);
        Assert.AreEqual("/", cookie.Cookie.Path);
        Assert.AreEqual(TimeSpan.FromHours(12), cookie.ExpireTimeSpan);
        Assert.IsFalse(cookie.SlidingExpiration);
        Assert.IsInstanceOfType<MemoryCacheTicketStore>(cookie.SessionStore);
        Assert.AreEqual("X-CSRF-TOKEN", antiforgery.HeaderName);
    }

    [TestMethod]
    public void AddPersonalAuthentication_UsesRequestSecurityForDevelopmentCookie()
    {
        var services = new ServiceCollection();
        services.AddPersonalAuthentication(TimeSpan.FromHours(12), isDevelopment: true);
        using var provider = services.BuildServiceProvider();

        var cookie = provider.GetRequiredService<IOptionsMonitor<CookieAuthenticationOptions>>()
            .Get(AttendanceAuthenticationDefaults.SchemeName);

        Assert.AreEqual("Personal.Session", cookie.Cookie.Name);
        Assert.AreEqual(CookieSecurePolicy.SameAsRequest, cookie.Cookie.SecurePolicy);
    }
}
