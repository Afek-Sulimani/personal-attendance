using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.RateLimiting;

namespace Personal.Attendance.Api.Authentication;

public static class AuthenticationServiceCollectionExtensions
{
    public static IServiceCollection AddPersonalAuthentication(
        this IServiceCollection services,
        TimeSpan sessionLifetime,
        bool isDevelopment)
    {
        ArgumentNullException.ThrowIfNull(services);
        if (sessionLifetime <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(sessionLifetime));
        }

        var cookieSecurePolicy = isDevelopment
            ? CookieSecurePolicy.SameAsRequest
            : CookieSecurePolicy.Always;
        var sessionCookieName = isDevelopment
            ? "Personal.Session"
            : "__Host-PersonalSession";
        var antiforgeryCookieName = isDevelopment
            ? "Personal.Antiforgery"
            : "__Host-Personal.Antiforgery";

        services.AddMemoryCache();
        services.AddSingleton<MemoryCacheTicketStore>();
        services.AddAuthentication(AttendanceAuthenticationDefaults.SchemeName)
            .AddCookie(AttendanceAuthenticationDefaults.SchemeName, options =>
            {
                options.Cookie.Name = sessionCookieName;
                options.Cookie.HttpOnly = true;
                options.Cookie.SecurePolicy = cookieSecurePolicy;
                options.Cookie.SameSite = SameSiteMode.Lax;
                options.Cookie.Path = "/";
                options.Cookie.IsEssential = true;
                options.ExpireTimeSpan = sessionLifetime;
                options.SlidingExpiration = false;
                options.Events.OnRedirectToLogin = context =>
                {
                    context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                    return Task.CompletedTask;
                };
                options.Events.OnRedirectToAccessDenied = context =>
                {
                    context.Response.StatusCode = StatusCodes.Status403Forbidden;
                    return Task.CompletedTask;
                };
            });
        services
            .AddOptions<CookieAuthenticationOptions>(AttendanceAuthenticationDefaults.SchemeName)
            .Configure<MemoryCacheTicketStore>((options, ticketStore) => options.SessionStore = ticketStore);
        services.AddAntiforgery(options =>
        {
            options.HeaderName = "X-CSRF-TOKEN";
            options.Cookie.Name = antiforgeryCookieName;
            options.Cookie.HttpOnly = true;
            options.Cookie.SecurePolicy = cookieSecurePolicy;
            options.Cookie.SameSite = SameSiteMode.Lax;
            options.Cookie.Path = "/";
        });
        services.AddAuthorization();
        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.AddPolicy(AttendanceAuthenticationDefaults.LoginRateLimitPolicyName, context =>
                RateLimitPartition.GetFixedWindowLimiter(
                    context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                    _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = 15,
                        Window = TimeSpan.FromMinutes(1),
                        QueueLimit = 0,
                        AutoReplenishment = true
                    }));
        });

        return services;
    }
}
