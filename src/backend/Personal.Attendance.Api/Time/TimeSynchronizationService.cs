using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Personal.Attendance.Application.Time;

namespace Personal.Attendance.Api.Time;

public sealed class TimeSynchronizationService(
    TimeSampleCoordinator sampleCoordinator,
    ICompanyTimeZoneRegistry timeZoneRegistry,
    TimeProvider timeProvider,
    IOptions<TimeSynchronizationOptions> options,
    ILogger<TimeSynchronizationService> logger) : BackgroundService
{
    private readonly TimeSynchronizationOptions _options = options.Value;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Record service startup before making the initial provider requests.
        logger.LogInformation("External time synchronization service started.");

        try
        {
            // Prime registered clocks immediately instead of making the first employee wait for a timer tick.
            await RefreshRegisteredClocksAsync(stoppingToken);

            // Use the configured TimeProvider for a cancellable periodic refresh schedule.
            using var timer = new PeriodicTimer(
                TimeSpan.FromMinutes(5),
                timeProvider);
            var lastHealthyRefresh = timeProvider.GetTimestamp();

            // Refresh each currently registered company time zone on every timer tick.
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                var elapsed = timeProvider.GetElapsedTime(lastHealthyRefresh);
                var fullRefresh = elapsed >= _options.RefreshInterval;
                await RefreshRegisteredClocksAsync(stoppingToken, fullRefresh);
                if (fullRefresh) lastHealthyRefresh = timeProvider.GetTimestamp();
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            logger.LogInformation("External time synchronization service stopped.");
        }
    }

    private async Task RefreshRegisteredClocksAsync(CancellationToken cancellationToken, bool forceRefresh = true)
    {
        // A company zone is registered when its payroll settings are read by the application.
        foreach (var timeZoneId in timeZoneRegistry.GetRegisteredTimeZones())
        {
            try
            {
                // Scheduled refreshes are forced, so a previous failure cooldown cannot skip this attempt.
                var refreshed = await sampleCoordinator.RefreshAsync(
                    timeZoneId,
                    forceRefresh: forceRefresh || sampleCoordinator.NeedsRecovery(timeZoneId),
                    cancellationToken);

                if (!refreshed)
                {
                    // Keep any previous sample only until its configured maximum age is reached.
                    logger.LogWarning(
                        "The {TimeZoneId} clock could not refresh. Its last sample remains usable only until it expires.",
                        timeZoneId);
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                // Let host shutdown stop the entire refresh loop promptly.
                throw;
            }
            catch (Exception exception)
            {
                // Log one zone's unexpected failure and continue refreshing the remaining zones.
                logger.LogError(exception, "Unexpected failure while refreshing the {TimeZoneId} clock.", timeZoneId);
            }
        }
    }
}
