using System.Diagnostics;
using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Personal.Attendance.Application.Time;

namespace Personal.Attendance.Infrastructure.Time;

public sealed class TimezoneIoSource(HttpClient httpClient) : ICurrentTimeSource
{
    private const string RequestUri = "https://worldtime.timezone.io/api/timezone/{0}";

    public string Id => "timezone-io";

    public async Task<CurrentTimeSample> GetCurrentTimeAsync(
        string timeZoneId,
        CancellationToken cancellationToken)
    {
        // Resolve the zone locally so an unsupported company setting fails before an external request.
        var timeZone = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);

        // Measure latency with a monotonic stopwatch for the sample's provenance and compensation estimate.
        var startedAt = Stopwatch.GetTimestamp();

        // Encode the zone path segment so the provider receives the intended IANA/system zone ID.
        var requestUri = string.Format(
            CultureInfo.InvariantCulture,
            RequestUri,
            Uri.EscapeDataString(timeZone.Id));

        // Fetch the fallback provider's JSON response using the same timeout configured for the primary source.
        var response = await httpClient.GetFromJsonAsync<TimezoneIoResponse>(
            requestUri,
            cancellationToken);

        // Capture duration immediately after the network call so it does not include our validation work.
        var duration = Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds;

        // Ensure the fallback did not answer for a different time zone.
        if (response is null ||
            !string.Equals(response.TimeZone, timeZone.Id, StringComparison.Ordinal))
        {
            throw new InvalidDataException("Timezone.io returned an unexpected time zone.");
        }

        // Parse both provider values invariantly; the local value includes its offset and the UTC value is absolute.
        if (!DateTimeOffset.TryParse(
                response.DateTime,
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out var localTime) ||
            !DateTimeOffset.TryParse(
                response.UtcDateTime,
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out var utcTime))
        {
            throw new InvalidDataException("Timezone.io returned an invalid date-time.");
        }

        // Normalize the provider instant to zero-offset UTC before comparing it with system zone rules.
        var normalizedUtcTime = utcTime.ToUniversalTime();
        var expectedOffset = timeZone.GetUtcOffset(normalizedUtcTime);
        var isDaylightSaving = timeZone.IsDaylightSavingTime(normalizedUtcTime);

        // Cross-check local/UTC equality, the expected offset, and DST to reject a self-contradictory response.
        if (localTime.ToString("zzz", CultureInfo.InvariantCulture) != response.UtcOffset ||
            Math.Abs((localTime.ToUniversalTime() - normalizedUtcTime).TotalSeconds) > 1 ||
            localTime.Offset != expectedOffset ||
            response.Dst != isDaylightSaving)
        {
            throw new InvalidDataException(
                "Timezone.io returned time values that do not match the requested time-zone rules.");
        }

        // Return validated time plus provider identity and request latency for the shared clock cache.
        return new CurrentTimeSample(
            "Timezone.io",
            timeZone.Id,
            localTime,
            normalizedUtcTime,
            response.UtcOffset,
            response.Dst,
            duration);
    }

    // Maps the provider's snake-case JSON fields to the properties used by validation above.
    private sealed record TimezoneIoResponse(
        [property: JsonPropertyName("datetime")] string DateTime,
        [property: JsonPropertyName("utc_datetime")] string UtcDateTime,
        [property: JsonPropertyName("timezone")] string TimeZone,
        [property: JsonPropertyName("utc_offset")] string UtcOffset,
        [property: JsonPropertyName("dst")] bool Dst);
}
