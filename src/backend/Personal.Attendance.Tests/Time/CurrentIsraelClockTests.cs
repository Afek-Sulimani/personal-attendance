using System.Globalization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Personal.Attendance.Api.Time;
using Personal.Attendance.Application.Time;
using Personal.Attendance.Infrastructure.Time;

namespace Personal.Attendance.Tests.Time;

[TestClass]
public sealed class CurrentIsraelClockTests
{
    [TestMethod]
    public async Task GetCurrentTimeAsync_UsesExistingSampleWithoutAnotherProviderCall()
    {
        var source = new CountingTimeSource(CreateSample());
        using var services = CreateServices(source);
        using var coordinator = CreateCoordinator(services, source.Id, out var synchronizedClock);
        var clock = new CurrentAttendanceClock(synchronizedClock, coordinator);
        var firstReading = await clock.GetCurrentTimeAsync("Asia/Jerusalem", CancellationToken.None);

        var reading = await clock.GetCurrentTimeAsync("Asia/Jerusalem", CancellationToken.None);

        Assert.AreEqual(1, source.CallCount);
        Assert.AreEqual(firstReading.UtcTime, reading.UtcTime);
    }

    [TestMethod]
    public async Task GetCurrentTimeAsync_RefreshesFromProviderWhenCacheIsEmpty()
    {
        var source = new CountingTimeSource(CreateSample());
        using var services = CreateServices(source);
        using var coordinator = CreateCoordinator(services, source.Id, out var synchronizedClock);
        var clock = new CurrentAttendanceClock(synchronizedClock, coordinator);

        var reading = await clock.GetCurrentTimeAsync("Asia/Jerusalem", CancellationToken.None);

        Assert.AreEqual(1, source.CallCount);
        Assert.AreEqual(source.Sample.Provider, reading.Provider);
    }

    [TestMethod]
    public async Task GetCurrentTimeAsync_ThrowsWhenNoExternalSourceIsAvailable()
    {
        var source = new CountingTimeSource(
            CreateSample(),
            new HttpRequestException("Provider unavailable."));
        using var services = CreateServices(source);
        using var coordinator = CreateCoordinator(services, source.Id, out var synchronizedClock);
        var clock = new CurrentAttendanceClock(synchronizedClock, coordinator);

        try
        {
            await clock.GetCurrentTimeAsync("Asia/Jerusalem", CancellationToken.None);
            Assert.Fail("Expected the clock to fail when all external sources are unavailable.");
        }
        catch (ClockUnavailableException)
        {
        }

        Assert.AreEqual(1, source.CallCount);
    }

    [TestMethod]
    public async Task GetCurrentTimeForAttendanceAsync_SharesRecentBackgroundFailure()
    {
        var source = new RecoveringTimeSource(CreateSample());
        using var services = CreateServices(source);
        using var coordinator = CreateCoordinator(services, source.Id, out var synchronizedClock);
        var clock = new CurrentAttendanceClock(synchronizedClock, coordinator);

        Assert.IsFalse(await coordinator.RefreshAsync(false, CancellationToken.None));
        Assert.AreEqual(1, source.CallCount);

        await Assert.ThrowsExactlyAsync<ClockUnavailableException>(() =>
            clock.GetCurrentTimeForAttendanceAsync("Asia/Jerusalem", CancellationToken.None));
        Assert.AreEqual(1, source.CallCount);
    }

    [TestMethod]
    public async Task RecoveryFlag_ClearsOnlyAfterSuccessfulProviderRefresh()
    {
        var source = new RecoveringTimeSource(CreateSample());
        using var services = CreateServices(source);
        using var coordinator = CreateCoordinator(services, source.Id, out _);
        Assert.IsFalse(await coordinator.RefreshAsync(true, CancellationToken.None));
        Assert.IsTrue(coordinator.NeedsRecovery("Asia/Jerusalem"));
        Assert.IsTrue(await coordinator.RefreshAsync(true, CancellationToken.None));
        Assert.IsFalse(coordinator.NeedsRecovery("Asia/Jerusalem"));
        Assert.AreEqual(2, source.CallCount);
    }

    private static ServiceProvider CreateServices(ICurrentTimeSource source)
    {
        var services = new ServiceCollection();
        services.AddSingleton<ICurrentTimeSource>(source);
        return services.BuildServiceProvider();
    }

    private static TimeSampleCoordinator CreateCoordinator(
        IServiceProvider services,
        string sourceId,
        out SynchronizedClock synchronizedClock)
    {
        var options = Options.Create(new TimeSynchronizationOptions
        {
            ProviderOrder = [sourceId]
        });
        var timeProvider = new FakeTimeProvider(Instant("2026-11-18T12:00:00Z"));
        var zone = TimeZoneInfo.FindSystemTimeZoneById("Asia/Jerusalem");
        synchronizedClock = new SynchronizedClock(
            timeProvider,
            zone,
            TimeSpan.FromMinutes(2));

        return new TimeSampleCoordinator(
            services.GetRequiredService<IServiceScopeFactory>(),
            synchronizedClock,
            timeProvider,
            options,
            NullLogger<TimeSampleCoordinator>.Instance);
    }

    private static CurrentTimeSample CreateSample()
    {
        var zone = TimeZoneInfo.FindSystemTimeZoneById("Asia/Jerusalem");
        var utcTime = Instant("2026-11-18T09:00:00Z");
        var localTime = TimeZoneInfo.ConvertTime(utcTime, zone);

        return new CurrentTimeSample(
            "test-provider",
            zone.Id,
            localTime,
            utcTime,
            localTime.ToString("zzz", CultureInfo.InvariantCulture),
            zone.IsDaylightSavingTime(utcTime),
            0);
    }

    private static DateTimeOffset Instant(string value) =>
        DateTimeOffset.Parse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal);

    private sealed class CountingTimeSource(
        CurrentTimeSample sample,
        Exception? exception = null) : ICurrentTimeSource
    {
        public string Id => "test-provider";

        public CurrentTimeSample Sample => sample;

        public int CallCount { get; private set; }

        public Task<CurrentTimeSample> GetCurrentTimeAsync(string timeZoneId, CancellationToken cancellationToken)
        {
            CallCount++;
            return exception is null
                ? Task.FromResult(sample)
                : Task.FromException<CurrentTimeSample>(exception);
        }
    }

    private sealed class RecoveringTimeSource(CurrentTimeSample sample) : ICurrentTimeSource
    {
        public string Id => "test-provider";
        public CurrentTimeSample Sample => sample;
        public int CallCount { get; private set; }

        public Task<CurrentTimeSample> GetCurrentTimeAsync(string timeZoneId, CancellationToken cancellationToken)
        {
            CallCount++;
            return CallCount == 1
                ? Task.FromException<CurrentTimeSample>(new HttpRequestException("Temporary provider failure."))
                : Task.FromResult(sample);
        }
    }
}
