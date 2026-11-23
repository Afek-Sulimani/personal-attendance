using Personal.Attendance.Application.Time;
using Personal.Attendance.Infrastructure.Time;

namespace Personal.Attendance.Api.Time;

public sealed class CurrentAttendanceClock(
    SynchronizedClock synchronizedClock,
    TimeSampleCoordinator sampleCoordinator) : ICurrentAttendanceClock
{
    // Non-attendance readers use the ordinary refresh cooldown when the cache is stale.
    public Task<ClockReading> GetCurrentTimeAsync(
        string timeZoneId,
        CancellationToken cancellationToken) =>
        GetCurrentTimeAsync(timeZoneId, forceExternalFallback: false, cancellationToken);

    // A live punch attempts external fallback immediately rather than trusting an expired sample.
    public Task<ClockReading> GetCurrentTimeForAttendanceAsync(
        string timeZoneId,
        CancellationToken cancellationToken) =>
        GetCurrentTimeAsync(timeZoneId, forceExternalFallback: true, cancellationToken);

    private async Task<ClockReading> GetCurrentTimeAsync(
        string timeZoneId,
        bool forceExternalFallback,
        CancellationToken cancellationToken)
    {
        // Reject unsupported IDs before they can become cache keys or provider request parameters.
        var timeZone = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);

        // Fast path: a fresh synchronized sample can serve this request without network traffic.
        if (synchronizedClock.TryGetCurrentTime(timeZone.Id, out var currentReading) &&
            currentReading is not null)
        {
            return currentReading;
        }

        // Select the stricter attendance fallback policy only for operations that record a punch.
        var refreshed = forceExternalFallback
            ? await sampleCoordinator.RefreshForAttendanceFallbackAsync(timeZone.Id, cancellationToken)
            : await sampleCoordinator.RefreshAsync(timeZone.Id, forceRefresh: false, cancellationToken);

        // Re-read the cache after refresh so the returned time includes elapsed time since the sample.
        if (refreshed &&
            synchronizedClock.TryGetCurrentTime(timeZone.Id, out currentReading) &&
            currentReading is not null)
        {
            return currentReading;
        }

        // Never replace a missing external sample with browser, server, or database time.
        throw new ClockUnavailableException();
    }
}
