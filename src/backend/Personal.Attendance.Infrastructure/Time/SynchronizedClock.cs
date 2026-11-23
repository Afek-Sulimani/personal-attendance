using Personal.Attendance.Application.Time;
using System.Collections.Concurrent;

namespace Personal.Attendance.Infrastructure.Time;

public sealed class SynchronizedClock
{
    private readonly TimeProvider _timeProvider;
    private readonly string _defaultTimeZoneId;
    private readonly TimeSpan _maximumSampleAge;

    // Keep one in-memory snapshot per company zone; attendance records themselves are stored separately in SQL.
    private readonly ConcurrentDictionary<string, ClockSnapshot> _snapshots = new(StringComparer.OrdinalIgnoreCase);

    public SynchronizedClock(
        TimeProvider timeProvider,
        TimeZoneInfo timeZone,
        TimeSpan maximumSampleAge)
    {
        // Fail during service setup rather than allowing a clock with incomplete timing rules to run.
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentNullException.ThrowIfNull(timeZone);

        // A zero or negative lifetime would make every provider sample unusable immediately.
        if (maximumSampleAge <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maximumSampleAge),
                "The maximum sample age must be greater than zero.");
        }

        _timeProvider = timeProvider;
        _defaultTimeZoneId = timeZone.Id;
        _maximumSampleAge = maximumSampleAge;
    }

    public void Publish(CurrentTimeSample sample)
    {
        // Do not let a provider bypass the coordinator's validation when publishing a cache entry.
        ArgumentNullException.ThrowIfNull(sample);

        var timeZone = TimeZoneInfo.FindSystemTimeZoneById(sample.TimeZone);
        // Use the operating system's canonical zone ID as the snapshot key.
        if (!string.Equals(sample.TimeZone, timeZone.Id, StringComparison.Ordinal))
        {
            throw new InvalidDataException("The sample must use a canonical system time-zone ID.");
        }

        // UTC with a zero offset keeps the absolute instant consistent across company zones.
        if (sample.UtcTime.Offset != TimeSpan.Zero)
        {
            throw new InvalidDataException("The sample UTC time must have a zero offset.");
        }

        // Invalid latency cannot be used safely to compensate for network delay.
        if (!double.IsFinite(sample.RequestDurationMilliseconds) ||
            sample.RequestDurationMilliseconds < 0)
        {
            throw new InvalidDataException("The sample request duration is invalid.");
        }

        // The provider timestamp occurs during the HTTP round trip, so half its duration estimates the midpoint.
        // This compensation reduces average network lag; it does not promise atomic-clock precision.
        var latencyCompensation = TimeSpan.FromMilliseconds(
            sample.RequestDurationMilliseconds / 2);

        // Save both the estimated receipt time and monotonic receipt tick to advance time without wall-clock drift.
        var snapshot = new ClockSnapshot(
            sample.UtcTime,
            sample.UtcTime + latencyCompensation,
            _timeProvider.GetTimestamp(),
            sample.Provider,
            sample.RequestDurationMilliseconds,
            latencyCompensation.TotalMilliseconds);

        // Replace the old zone snapshot atomically so readers see either the old sample or the new one.
        _snapshots[timeZone.Id] = snapshot;
    }

    public bool TryGetCurrentTime(out ClockReading? reading) =>
        TryGetCurrentTime(_defaultTimeZoneId, out reading);

    public bool TryGetCurrentTime(string timeZoneId, out ClockReading? reading)
    {
        // Resolve aliases before querying the dictionary so equivalent IDs share the same snapshot.
        var timeZone = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);

        // A missing snapshot means startup or refresh has not yet produced an external sample for this zone.
        if (!_snapshots.TryGetValue(timeZone.Id, out var snapshot))
        {
            reading = null;
            return false;
        }

        // Measure elapsed duration with the monotonic clock, not the machine's changeable wall-clock time.
        var currentTimestamp = _timeProvider.GetTimestamp();
        var sampleAge = _timeProvider.GetElapsedTime(
            snapshot.MonotonicTimestamp,
            currentTimestamp);

        // Reject clock resets and expired samples so a punch can trigger a new external-provider request.
        if (sampleAge < TimeSpan.Zero || sampleAge > _maximumSampleAge)
        {
            reading = null;
            return false;
        }

        // Advance the externally anchored UTC instant by monotonic elapsed time, then derive company-local time.
        var currentUtcTime = snapshot.EstimatedUtcAtReceipt + sampleAge;
        var currentLocalTime = TimeZoneInfo.ConvertTime(currentUtcTime, timeZone);

        // Return the calculated time together with source metadata needed for audit and database provenance.
        reading = new ClockReading(
            timeZone.Id,
            snapshot.Provider,
            currentUtcTime,
            currentLocalTime,
            snapshot.ProviderSampleUtcTime,
            sampleAge.TotalMilliseconds,
            snapshot.ProviderRequestDurationMilliseconds,
            snapshot.LatencyCompensationMilliseconds);

        return true;
    }

    private sealed record ClockSnapshot(
        DateTimeOffset ProviderSampleUtcTime,
        DateTimeOffset EstimatedUtcAtReceipt,
        long MonotonicTimestamp,
        string Provider,
        double ProviderRequestDurationMilliseconds,
        double LatencyCompensationMilliseconds);
}
