using System.Collections.Concurrent;
using Microsoft.Extensions.Options;
using Personal.Attendance.Application.Time;

namespace Personal.Attendance.Api.Time;

public sealed class CompanyTimeZoneRegistry : ICompanyTimeZoneRegistry
{
    // Tracks each zone once so the background service refreshes one clock per distinct zone.
    private readonly ConcurrentDictionary<string, byte> _timeZones = new(StringComparer.OrdinalIgnoreCase);

    public CompanyTimeZoneRegistry(IOptions<TimeSynchronizationOptions> options)
    {
        // Register the configured default before the first background refresh starts.
        DefaultTimeZoneId = Register(options.Value.TimeZoneId);
    }

    public string DefaultTimeZoneId { get; }

    public string Register(string timeZoneId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(timeZoneId);

        // Resolve aliases to the operating system's canonical ID before using it as a cache key.
        var canonicalId = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId.Trim()).Id;

        // TryAdd makes registration safe when multiple requests discover the same company zone.
        _timeZones.TryAdd(canonicalId, 0);
        return canonicalId;
    }

    // Return a stable snapshot so callers can iterate while other requests register new zones.
    public IReadOnlyList<string> GetRegisteredTimeZones() =>
        _timeZones.Keys.Order(StringComparer.Ordinal).ToArray();
}
