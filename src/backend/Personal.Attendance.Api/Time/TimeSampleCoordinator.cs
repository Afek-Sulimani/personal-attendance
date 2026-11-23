using System.Globalization;
using System.Collections.Concurrent;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Personal.Attendance.Application.Time;
using Personal.Attendance.Infrastructure.Time;

namespace Personal.Attendance.Api.Time;

public sealed class TimeSampleCoordinator : IDisposable
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly SynchronizedClock _synchronizedClock;
    private readonly TimeProvider _timeProvider;
    private readonly TimeSynchronizationOptions _options;
    private readonly ILogger<TimeSampleCoordinator> _logger;

    // Attendance failures use a short cooldown shared by waiting punch requests for the same zone.
    private readonly ConcurrentDictionary<string, long> _lastAttendanceFailure = new();

    public bool NeedsRecovery(string zone) => _lastAttendanceFailure.ContainsKey(zone);

    // Serialize provider chains so concurrent callers cannot fan out duplicate requests or race to publish samples.
    private readonly SemaphoreSlim _refreshLock = new(1, 1);

    // Ordinary refreshes keep their own longer cooldown, separate from attendance fallback attempts.
    private readonly ConcurrentDictionary<string, long> _lastRefreshAttemptTimestamps =
        new(StringComparer.OrdinalIgnoreCase);

    public TimeSampleCoordinator(
        IServiceScopeFactory scopeFactory,
        SynchronizedClock synchronizedClock,
        TimeProvider timeProvider,
        IOptions<TimeSynchronizationOptions> options,
        ILogger<TimeSampleCoordinator> logger)
    {
        _scopeFactory = scopeFactory;
        _synchronizedClock = synchronizedClock;
        _timeProvider = timeProvider;
        _options = options.Value;
        _logger = logger;
    }

    // Preserve a default-zone entry point for callers that do not have a company-specific zone.
    public Task<bool> RefreshAsync(
        bool forceRefresh,
        CancellationToken cancellationToken) =>
        RefreshAsync(_options.TimeZoneId, forceRefresh, cancellationToken);

    // Provide the configured default zone for callers that do not have a company-specific zone.
    public Task<bool> RefreshForAttendanceFallbackAsync(CancellationToken cancellationToken) =>
        RefreshForAttendanceFallbackAsync(_options.TimeZoneId, cancellationToken);

    // Ordinary refreshes honor cooldowns; forced background refreshes bypass them.
    public Task<bool> RefreshAsync(
        string timeZoneId,
        bool forceRefresh,
        CancellationToken cancellationToken) =>
        RefreshAsync(timeZoneId, forceRefresh, bypassFailureCooldown: false, cancellationToken);

    // Punch fallbacks use the shorter shared attendance-failure cooldown.
    public Task<bool> RefreshForAttendanceFallbackAsync(
        string timeZoneId,
        CancellationToken cancellationToken) =>
        RefreshAsync(timeZoneId, forceRefresh: false, bypassFailureCooldown: true, cancellationToken);

    private async Task<bool> RefreshAsync(
        string timeZoneId,
        bool forceRefresh,
        bool bypassFailureCooldown,
        CancellationToken cancellationToken)
    {
        // Normalize the requested ID so cache, registry, and provider lookups use one canonical key.
        var timeZone = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);

        // Avoid taking the global refresh lock when a previous request already supplied a fresh sample.
        if (!forceRefresh && _synchronizedClock.TryGetCurrentTime(timeZone.Id, out _))
        {
            return true;
        }

        await _refreshLock.WaitAsync(cancellationToken);

        try
        {
            // Recheck after acquiring the lock because another request may have refreshed while we waited.
            if (!forceRefresh && _synchronizedClock.TryGetCurrentTime(timeZone.Id, out _))
            {
                return true;
            }

            // Use monotonic timestamps for cooldown durations, independent of wall-clock adjustments.
            var currentTimestamp = _timeProvider.GetTimestamp();

            // Share a recent all-provider attendance failure instead of queuing another external chain.
            if (bypassFailureCooldown && _lastAttendanceFailure.TryGetValue(timeZone.Id, out var failedAt) &&
                _timeProvider.GetElapsedTime(failedAt, currentTimestamp) < TimeSpan.FromSeconds(5))
            {
                return false;
            }
            // Ordinary callers also avoid repeating a failed refresh until the configured cooldown expires.
            if (!forceRefresh && !bypassFailureCooldown &&
                _lastRefreshAttemptTimestamps.TryGetValue(timeZone.Id, out var lastRefreshAttemptTimestamp) &&
                _timeProvider.GetElapsedTime(lastRefreshAttemptTimestamp, currentTimestamp) < _options.RefreshFailureCooldown)
            {
                return false;
            }

            // Mark the attempt before calling providers so concurrent ordinary callers see the cooldown.
            _lastRefreshAttemptTimestamps[timeZone.Id] = currentTimestamp;

            // Resolve fresh typed clients inside a short-lived scope and dispose them after this refresh.
            await using var scope = _scopeFactory.CreateAsyncScope();
            var sources = scope.ServiceProvider
                .GetServices<ICurrentTimeSource>()
                .ToArray();

            // Follow configuration order so the primary provider is always attempted before its fallback.
            foreach (var sourceId in _options.ProviderOrder)
            {
                // Resolve the configured provider ID from the registered time-source implementations.
                var source = sources.FirstOrDefault(candidate =>
                    string.Equals(
                        candidate.Id,
                        sourceId,
                        StringComparison.OrdinalIgnoreCase));

                if (source is null)
                {
                    // A missing registration should not prevent a later configured fallback from running.
                    _logger.LogWarning(
                        "Configured time source {SourceId} is not registered.",
                        sourceId);
                    continue;
                }

                try
                {
                    // Fetch, validate, and publish as one attempt; only a validated sample can update the clock.
                    var sample = await source.GetCurrentTimeAsync(timeZone.Id, cancellationToken);
                    ValidateSample(sample, timeZone);
                    _synchronizedClock.Publish(sample);
                    _lastAttendanceFailure.TryRemove(timeZone.Id, out _);

                    _logger.LogInformation(
                        "Updated the {TimeZoneId} clock from {Provider} in {DurationMs} ms.",
                        timeZone.Id,
                        sample.Provider,
                        sample.RequestDurationMilliseconds);

                    // Stop after the first valid provider response; later providers are fallback-only.
                    return true;
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    // Cancellation belongs to the caller or host and must not be treated as provider failure.
                    throw;
                }
                catch (Exception exception)
                {
                    // A timeout, network error, or invalid sample falls through to the next provider.
                    _logger.LogWarning(
                        exception,
                        "Time source {SourceId} failed validation or did not respond.",
                        sourceId);
                }
            }

            // Record one shared attendance failure timestamp after every configured provider has failed.
            _lastAttendanceFailure[timeZone.Id] = _timeProvider.GetTimestamp();
            _logger.LogWarning(
                "No configured time source could provide a valid time sample for {TimeZoneId}.",
                timeZone.Id);

            return false;
        }
        finally
        {
            // Release the lock on success, provider failure, or cancellation.
            _refreshLock.Release();
        }
    }

    public void Dispose()
    {
        _refreshLock.Dispose();
    }

    private void ValidateSample(CurrentTimeSample sample, TimeZoneInfo timeZone)
    {
        // Require an exact zone match; accepting a different zone would shift the employee's work date.
        if (!string.Equals(
                sample.TimeZone,
                timeZone.Id,
                StringComparison.Ordinal))
        {
            throw new InvalidDataException("The provider returned an unexpected time zone.");
        }

        // Keep the stored instant unambiguous by requiring UTC to have a zero offset.
        if (sample.UtcTime.Offset != TimeSpan.Zero)
        {
            throw new InvalidDataException("The provider UTC value has a non-zero offset.");
        }

        // Reject impossible or over-timeout durations before they influence the sample-age estimate.
        if (!double.IsFinite(sample.RequestDurationMilliseconds) ||
            sample.RequestDurationMilliseconds < 0 ||
            sample.RequestDurationMilliseconds > _options.ProviderTimeout.TotalMilliseconds)
        {
            throw new InvalidDataException("The provider response duration is outside the limit.");
        }

        // Recompute local values from UTC and platform time-zone rules to cross-check provider data.
        var expectedLocalTime = TimeZoneInfo.ConvertTime(sample.UtcTime, timeZone);
        var localUtcDifference = Math.Abs(
            (sample.LocalTime.ToUniversalTime() - sample.UtcTime).TotalSeconds);

        // Allow at most one second of local/UTC rounding, while requiring offset and DST to match exactly.
        if (localUtcDifference > 1 ||
            sample.LocalTime.Offset != expectedLocalTime.Offset ||
            !string.Equals(
                sample.UtcOffset,
                expectedLocalTime.ToString("zzz", CultureInfo.InvariantCulture),
                StringComparison.Ordinal) ||
            sample.IsDaylightSaving != timeZone.IsDaylightSavingTime(sample.UtcTime))
        {
            throw new InvalidDataException(
                "The provider local time, offset, or daylight-saving value is inconsistent.");
        }
    }
}
