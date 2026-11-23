namespace Personal.Attendance.Api.Time;

public sealed class TimeSynchronizationOptions
{
    // Names the appsettings section that supplies the time synchronization settings.
    public const string SectionName = "TimeSynchronization";

    // Supplies the fallback zone for companies that have not configured their own zone.
    public string TimeZoneId { get; set; } = "Asia/Jerusalem";

    // Defines which providers are tried and in what order when a new sample is needed.
    public string[] ProviderOrder { get; set; } = [];

    // Sets how often the background service requests fresh provider samples.
    public TimeSpan RefreshInterval { get; set; } = TimeSpan.FromHours(1);

    // Marks a cached sample as stale after this much monotonic elapsed time.
    public TimeSpan MaximumSampleAge { get; set; } = TimeSpan.FromMinutes(65);

    // Caps the duration of each individual external provider request.
    public TimeSpan ProviderTimeout { get; set; } = TimeSpan.FromSeconds(5);

    // Prevents ordinary, non-forced refresh callers from immediately repeating a failed attempt.
    public TimeSpan RefreshFailureCooldown { get; set; } = TimeSpan.FromMinutes(5);
}
