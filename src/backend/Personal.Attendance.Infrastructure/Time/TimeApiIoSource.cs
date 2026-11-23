using System.Diagnostics;
using System.Globalization;
using System.Net.Http.Json;
using Personal.Attendance.Application.Time;

namespace Personal.Attendance.Infrastructure.Time;

public sealed class TimeApiIoSource(HttpClient httpClient) : ICurrentTimeSource
{
    private const string RequestUri = "https://timeapi.io/api/Time/current/zone?timeZone={0}";

    public string Id => "timeapi-io";

    public async Task<CurrentTimeSample> GetCurrentTimeAsync(
        string timeZoneId,
        CancellationToken cancellationToken)
    {
        // Resolve the zone locally so the request and every later check use a supported canonical ID.
        var timeZone = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);

        // Measure the provider round trip with Stopwatch, which is independent of wall-clock changes.
        var startedAt = Stopwatch.GetTimestamp();

        // URL-encode the zone ID because names such as Asia/Jerusalem contain reserved URL characters.
        var requestUri = string.Format(
            CultureInfo.InvariantCulture,
            RequestUri,
            Uri.EscapeDataString(timeZone.Id));
        // Fetch and deserialize the JSON response; HttpClient enforces the configured provider timeout.
        var response = await httpClient.GetFromJsonAsync<TimeApiIoResponse>(requestUri, cancellationToken);

        // Keep the measured duration with the sample for validation and latency compensation.
        var duration = Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds;

        // Refuse a response for another zone instead of silently shifting the company's local date.
        if (response is null || !string.Equals(response.TimeZone, timeZone.Id, StringComparison.Ordinal))
        {
            throw new InvalidDataException("TimeAPI.io returned an unexpected time zone.");
        }

        // Parse using invariant rules so the server's current culture cannot change date interpretation.
        if (!DateTime.TryParse(
                response.DateTime,
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out var parsedLocalTime))
        {
            throw new InvalidDataException("TimeAPI.io returned an invalid date-time.");
        }

        // Treat the provider's value as local wall time; it has no UTC offset until zone rules are applied.
        var localTime = DateTime.SpecifyKind(parsedLocalTime, DateTimeKind.Unspecified);

        // A spring-forward gap contains local times that never happened in this time zone.
        if (timeZone.IsInvalidTime(localTime))
        {
            throw new InvalidDataException("TimeAPI.io returned a skipped local time.");
        }

        DateTimeOffset localTimeWithOffset;
        if (timeZone.IsAmbiguousTime(localTime))
        {
            // During a fall-back overlap, choose the offset whose DST state matches the provider's DST flag.
            var matchingOffsets = timeZone.GetAmbiguousTimeOffsets(localTime)
                .Select(offset => new DateTimeOffset(localTime, offset))
                .Where(candidate => timeZone.IsDaylightSavingTime(candidate) == response.DstActive)
                .ToArray();

            if (matchingOffsets.Length != 1)
            {
                throw new InvalidDataException("TimeAPI.io did not disambiguate the daylight-saving time.");
            }

            // Exactly one matching offset turns the ambiguous local value into one unambiguous instant.
            localTimeWithOffset = matchingOffsets[0];
        }
        else
        {
            // Outside an overlap, the time-zone database supplies the one valid UTC offset.
            var offset = timeZone.GetUtcOffset(localTime);
            localTimeWithOffset = new DateTimeOffset(localTime, offset);

            // Cross-check the provider's DST flag against the platform's rules before accepting the sample.
            if (timeZone.IsDaylightSavingTime(localTimeWithOffset) != response.DstActive)
            {
                throw new InvalidDataException("TimeAPI.io daylight-saving information does not match the time-zone rules.");
            }
        }

        // Return both local and UTC forms so storage and local-date decisions use the same provider instant.
        return new CurrentTimeSample(
            "TimeAPI.io",
            timeZone.Id,
            localTimeWithOffset,
            localTimeWithOffset.ToUniversalTime(),
            localTimeWithOffset.ToString("zzz", CultureInfo.InvariantCulture),
            response.DstActive,
            duration);
    }

    private sealed record TimeApiIoResponse(string DateTime, string TimeZone, bool DstActive);
}
