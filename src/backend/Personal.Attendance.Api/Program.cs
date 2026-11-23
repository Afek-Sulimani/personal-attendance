using Personal.Attendance.Application.Time;
using Personal.Attendance.Application.Attendance;
using Personal.Attendance.Infrastructure.Time;
using Personal.Attendance.Api.Time;
using Personal.Attendance.Api.Attendance;
using Personal.Attendance.Api.Authentication;
using Personal.Attendance.Api.Invitations;
using Personal.Attendance.Api.Companies;
using Personal.Attendance.Api.Payroll;
using Personal.Attendance.Application.Authentication;
using Personal.Attendance.Application.Invitations;
using Personal.Attendance.Application.Companies;
using Personal.Attendance.Application.Payroll;
using Personal.Attendance.Application.Profiles;
using Personal.Attendance.Infrastructure.Attendance;
using Personal.Attendance.Infrastructure.Authentication;
using Personal.Attendance.Infrastructure.Invitations;
using Personal.Attendance.Infrastructure.Companies;
using Personal.Attendance.Infrastructure.Payroll;
using Personal.Attendance.Infrastructure.Profiles;
using Personal.Attendance.Api.Profiles;
using Personal.Attendance.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);

// Registers the built-in antiforgery action filter used by the cookie-authenticated API.
builder.Services.AddControllersWithViews();

// Read the configured session duration, using a 12-hour lifetime when local configuration omits it.
var sessionLifetime = builder.Configuration.GetValue<TimeSpan>(
    "Authentication:SessionLifetime",
    TimeSpan.FromHours(12));
builder.Services.AddPersonalAuthentication(sessionLifetime, builder.Environment.IsDevelopment());

// OAuth credentials are supplied through user secrets or environment variables.
var google = builder.Configuration.GetSection("Authentication:Google");
if (!string.IsNullOrWhiteSpace(google["ClientId"]) && !string.IsNullOrWhiteSpace(google["ClientSecret"]))
{
    builder.Services.AddAuthentication()
        .AddCookie("GoogleExternal", options => { options.ExpireTimeSpan = TimeSpan.FromMinutes(5); })
        .AddGoogle(options =>
        {
            options.ClientId = google["ClientId"]!;
            options.ClientSecret = google["ClientSecret"]!;
            options.SignInScheme = "GoogleExternal";
            options.CallbackPath = "/api/auth/google/callback";
            options.Events.OnCreatingTicket = context =>
            {
                if (!context.User.TryGetProperty("verified_email", out var verified) || !verified.GetBoolean())
                    throw new InvalidOperationException("Google email must be verified.");
                return Task.CompletedTask;
            };
            options.Events.OnRemoteFailure = context =>
            {
                context.HandleResponse();
                context.Response.Redirect("/login?googleError=authentication-failed");
                return Task.CompletedTask;
            };
        });
}

// Enable SQL-backed Identity and feature services only when an attendance database is configured.
var attendanceConnectionString = builder.Configuration.GetConnectionString("Attendance");
if (!string.IsNullOrWhiteSpace(attendanceConnectionString))
{
    builder.Services.AddDbContext<PersonalAttendanceDbContext>(options =>
        options.UseSqlServer(attendanceConnectionString));

    builder.Services.AddIdentityCore<PersonalUser>(options =>
    {
        options.User.RequireUniqueEmail = true;

        // Keep the existing ASP.NET Core Identity defaults explicit for activation and password changes.
        options.Password.RequiredLength = 6;
        options.Password.RequiredUniqueChars = 1;
        options.Password.RequireDigit = true;
        options.Password.RequireLowercase = true;
        options.Password.RequireUppercase = true;
        options.Password.RequireNonAlphanumeric = true;
    })
        .AddRoles<PersonalRole>()
        .AddEntityFrameworkStores<PersonalAttendanceDbContext>()
        .AddDefaultTokenProviders();

    builder.Services.AddScoped<IEmployeeAuthenticationService, EmployeeAuthenticationService>();
    builder.Services.AddScoped<IAttendanceService, AttendanceService>();
    builder.Services.AddScoped<IAdminAttendanceService, AdminAttendanceService>();
    builder.Services.AddScoped<IMonthlyAttendanceReportService, MonthlyAttendanceReportService>();
    builder.Services.AddScoped<IAttendanceCorrectionService, AttendanceCorrectionService>();
    builder.Services.AddScoped<IEmployeeInvitationService, EmployeeInvitationService>();
    builder.Services.AddScoped<ICompanyRegistrationService, CompanyRegistrationService>();
    builder.Services.AddScoped<IPayrollWorkScheduleService, PayrollWorkScheduleService>();
    builder.Services.AddScoped<ICompanyPayrollSettingsService, CompanyPayrollSettingsService>();
    builder.Services.AddScoped<IEmployeeProfileService, EmployeeProfileService>();
}
else
{
    // Keep the API host buildable without SQL Server while making data-dependent endpoints report unavailable.
    builder.Services.AddSingleton<IEmployeeAuthenticationService, UnavailableEmployeeAuthenticationService>();
    builder.Services.AddSingleton<IAttendanceService, UnavailableAttendanceService>();
    builder.Services.AddSingleton<IAdminAttendanceService, UnavailableAdminAttendanceService>();
    builder.Services.AddSingleton<IMonthlyAttendanceReportService, UnavailableMonthlyAttendanceReportService>();
    builder.Services.AddSingleton<IAttendanceCorrectionService, UnavailableAttendanceCorrectionService>();
    builder.Services.AddSingleton<IEmployeeInvitationService, UnavailableEmployeeInvitationService>();
    builder.Services.AddSingleton<ICompanyRegistrationService, UnavailableCompanyRegistrationService>();
    builder.Services.AddSingleton<IPayrollWorkScheduleService, UnavailablePayrollWorkScheduleService>();
    builder.Services.AddSingleton<ICompanyPayrollSettingsService, UnavailableCompanyPayrollSettingsService>();
    builder.Services.AddSingleton<IEmployeeProfileService, UnavailableEmployeeProfileService>();
}

// Validate the public invitation base URL at startup so generated activation links cannot use unsafe origins.
builder.Services.AddOptions<InvitationLinkOptions>()
    .Bind(builder.Configuration.GetSection(InvitationLinkOptions.SectionName))
    .Validate(options => Uri.TryCreate(options.PublicBaseUrl, UriKind.Absolute, out var publicBaseUri) &&
                         string.IsNullOrEmpty(publicBaseUri.UserInfo) &&
                         string.IsNullOrEmpty(publicBaseUri.Query) &&
                         string.IsNullOrEmpty(publicBaseUri.Fragment) &&
                         (publicBaseUri.Scheme == Uri.UriSchemeHttps ||
                          (builder.Environment.IsDevelopment() && publicBaseUri.Scheme == Uri.UriSchemeHttp)),
        "InvitationLinks:PublicBaseUrl must be an absolute HTTPS URL; HTTP is allowed only in Development.")
    .ValidateOnStart();

// Bind external-time settings and fail startup early if the zone, provider order, or refresh policy is invalid.
builder.Services.AddOptions<TimeSynchronizationOptions>()
    .Bind(builder.Configuration.GetSection(TimeSynchronizationOptions.SectionName))
    .Validate(options => IsSupportedTimeZone(options.TimeZoneId),
        "The default time zone must be a supported system time zone ID.")
    // Require distinct provider names so fallback order has no repeated source.
    .Validate(options => options.ProviderOrder is { Length: > 0 } &&
        options.ProviderOrder.All(sourceId => !string.IsNullOrWhiteSpace(sourceId)) &&
        options.ProviderOrder.Distinct(StringComparer.OrdinalIgnoreCase).Count() ==
        options.ProviderOrder.Length,
        "Configure one or more unique time source identifiers.")
    .Validate(options => options.RefreshInterval > TimeSpan.Zero,
        "The refresh interval must be greater than zero.")
    .Validate(options => options.MaximumSampleAge > TimeSpan.Zero,
        "The maximum sample age must be greater than zero.")
    .Validate(options => options.MaximumSampleAge > options.RefreshInterval,
        "The maximum sample age must exceed the refresh interval.")
    .Validate(options => options.ProviderTimeout > TimeSpan.Zero,
        "The provider timeout must be greater than zero.")
    .Validate(options => options.RefreshFailureCooldown > TimeSpan.Zero,
        "The refresh failure cooldown must be greater than zero.")
    .ValidateOnStart();

// Share the platform TimeProvider for monotonic elapsed-time measurements and hosted-service timers.
builder.Services.AddSingleton(TimeProvider.System);

// Track the default zone immediately and add company-specific zones when they are read.
builder.Services.AddSingleton<ICompanyTimeZoneRegistry, CompanyTimeZoneRegistry>();

// Create typed HTTP clients and apply the same per-provider timeout from configuration.
builder.Services.AddHttpClient<TimeApiIoSource>((services, client) =>
{
    client.Timeout = services.GetRequiredService<IOptions<TimeSynchronizationOptions>>()
        .Value.ProviderTimeout;
});
// Configure the fallback client independently so it receives the same timeout policy.
builder.Services.AddHttpClient<TimezoneIoSource>((services, client) =>
{
    client.Timeout = services.GetRequiredService<IOptions<TimeSynchronizationOptions>>()
        .Value.ProviderTimeout;
});

// Expose both typed clients through one interface so the coordinator can follow configured provider order.
builder.Services.AddTransient<ICurrentTimeSource>(services =>
    services.GetRequiredService<TimeApiIoSource>());
builder.Services.AddTransient<ICurrentTimeSource>(services =>
    services.GetRequiredService<TimezoneIoSource>());

// Keep one synchronized in-memory clock for the API process and apply the configured sample lifetime.
builder.Services.AddSingleton(services =>
{
    var options = services.GetRequiredService<IOptions<TimeSynchronizationOptions>>().Value;
    var timeZone = TimeZoneInfo.FindSystemTimeZoneById(options.TimeZoneId);

    return new SynchronizedClock(
        services.GetRequiredService<TimeProvider>(),
        timeZone,
        options.MaximumSampleAge);
});
// Share the refresh lock and failure cooldowns across requests in this API process.
builder.Services.AddSingleton<TimeSampleCoordinator>();

// Make one clock abstraction available to attendance services and other API endpoints.
builder.Services.AddSingleton<ICurrentAttendanceClock, CurrentAttendanceClock>();

// Start the configured external-time refresh loop with the web host.
builder.Services.AddHostedService<TimeSynchronizationService>();

var app = builder.Build();

app.UseRouting();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();

static bool IsSupportedTimeZone(string timeZoneId)
{
    try
    {
        _ = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
        return true;
    }
    catch (TimeZoneNotFoundException)
    {
        return false;
    }
    catch (InvalidTimeZoneException)
    {
        return false;
    }
}
