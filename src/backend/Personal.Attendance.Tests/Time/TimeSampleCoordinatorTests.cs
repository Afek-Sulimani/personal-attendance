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
public sealed class TimeSampleCoordinatorTests
{
    private static readonly TimeZoneInfo IsraelTimeZone =
        TimeZoneInfo.FindSystemTimeZoneById("Asia/Jerusalem");

    [TestMethod]
    public async Task RefreshAsync_UsesPrimaryProviderAndSkipsFallbackOnSuccess()
    {
        var primary = FakeTimeSource.Returning("primary", CreateSample());
        var fallback = FakeTimeSource.Returning("fallback", CreateSample("fallback-provider"));
        using var provider = CreateServiceProvider(primary, fallback);
        using var coordinator = CreateCoordinator(provider, "primary", "fallback");

        var refreshed = await coordinator.RefreshAsync(true, CancellationToken.None);

        Assert.IsTrue(refreshed);
        Assert.AreEqual(1, primary.CallCount);
        Assert.AreEqual(0, fallback.CallCount);
    }

    [TestMethod]
    public async Task RefreshAsync_UsesFallbackWhenPrimaryProviderFails()
    {
        var primary = FakeTimeSource.Failing("primary", new HttpRequestException("Unavailable"));
        var fallback = FakeTimeSource.Returning("fallback", CreateSample("fallback-provider"));
        using var provider = CreateServiceProvider(primary, fallback);
        using var coordinator = CreateCoordinator(provider, "primary", "fallback");

        var refreshed = await coordinator.RefreshAsync(true, CancellationToken.None);

        Assert.IsTrue(refreshed);
        Assert.AreEqual(1, primary.CallCount);
        Assert.AreEqual(1, fallback.CallCount);
    }

    [TestMethod]
    public async Task RefreshAsync_UsesFallbackWhenPrimarySampleIsInvalid()
    {
        var invalidPrimarySample = CreateSample() with { TimeZone = "UTC" };
        var primary = FakeTimeSource.Returning("primary", invalidPrimarySample);
        var fallback = FakeTimeSource.Returning("fallback", CreateSample("fallback-provider"));
        using var provider = CreateServiceProvider(primary, fallback);
        using var coordinator = CreateCoordinator(provider, "primary", "fallback");

        var refreshed = await coordinator.RefreshAsync(true, CancellationToken.None);

        Assert.IsTrue(refreshed);
        Assert.AreEqual(1, primary.CallCount);
        Assert.AreEqual(1, fallback.CallCount);
    }

    [TestMethod]
    public async Task RefreshAsync_ReturnsFalseAndKeepsLastSampleWhenAllProvidersFail()
    {
        var timeProvider = new FakeTimeProvider(Instant("2026-11-18T12:00:00Z"));
        var clock = new SynchronizedClock(
            timeProvider,
            IsraelTimeZone,
            TimeSpan.FromMinutes(2));
        clock.Publish(CreateSample("existing-provider"));

        var primary = FakeTimeSource.Failing("primary", new HttpRequestException("Unavailable"));
        var fallback = FakeTimeSource.Failing("fallback", new InvalidDataException("Invalid data"));
        using var provider = CreateServiceProvider(primary, fallback);
        using var coordinator = CreateCoordinator(provider, ["primary", "fallback"], clock, timeProvider);

        var refreshed = await coordinator.RefreshAsync(true, CancellationToken.None);
        var hasReading = clock.TryGetCurrentTime(out var reading);

        Assert.IsFalse(refreshed);
        Assert.IsTrue(hasReading);
        Assert.IsNotNull(reading);
        Assert.AreEqual("existing-provider", reading.Provider);
    }

    [TestMethod]
    public async Task RefreshAsync_ThrottlesRepeatedFailedProviderRequests()
    {
        var timeProvider = new FakeTimeProvider(Instant("2026-11-18T12:00:00Z"));
        var source = FakeTimeSource.Failing("primary", new HttpRequestException("Unavailable"));
        using var provider = CreateServiceProvider(source);
        var clock = new SynchronizedClock(timeProvider, IsraelTimeZone, TimeSpan.FromMinutes(2));
        using var coordinator = CreateCoordinator(provider, ["primary"], clock, timeProvider);

        Assert.IsFalse(await coordinator.RefreshAsync(false, CancellationToken.None));
        Assert.IsFalse(await coordinator.RefreshAsync(false, CancellationToken.None));
        Assert.AreEqual(1, source.CallCount);

        timeProvider.Advance(TimeSpan.FromMinutes(5));

        Assert.IsFalse(await coordinator.RefreshAsync(false, CancellationToken.None));
        Assert.AreEqual(2, source.CallCount);
    }

    [TestMethod]
    public async Task RefreshForAttendanceFallbackAsync_ReusesAReadingPublishedByAConcurrentRequest()
    {
        var pendingSample = new TaskCompletionSource<CurrentTimeSample>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var source = FakeTimeSource.Calling("primary", _ => pendingSample.Task);
        using var provider = CreateServiceProvider(source);
        using var coordinator = CreateCoordinator(provider, "primary");

        var firstRequest = coordinator.RefreshForAttendanceFallbackAsync(CancellationToken.None);
        Assert.AreEqual(1, source.CallCount);

        var secondRequest = coordinator.RefreshForAttendanceFallbackAsync(CancellationToken.None);
        pendingSample.SetResult(CreateSample());
        var results = await Task.WhenAll(firstRequest, secondRequest);

        CollectionAssert.AreEqual(new[] { true, true }, results);
        Assert.AreEqual(1, source.CallCount);
    }

    [TestMethod]
    public async Task AttendanceFallback_SharesFailureAndRetriesAfterShortCooldown()
    {
        var source = FakeTimeSource.Calling("primary", _ => Task.FromException<CurrentTimeSample>(new IOException("offline")));
        using var provider = CreateServiceProvider(source);
        var timeProvider = new FakeTimeProvider(Instant("2026-11-18T12:00:00Z"));
        using var coordinator = CreateCoordinator(provider, ["primary"], new SynchronizedClock(timeProvider, IsraelTimeZone, TimeSpan.FromMinutes(2)), timeProvider);
        Assert.IsFalse(await coordinator.RefreshForAttendanceFallbackAsync(CancellationToken.None));
        Assert.IsFalse(await coordinator.RefreshForAttendanceFallbackAsync(CancellationToken.None));
        Assert.AreEqual(1, source.CallCount);
        timeProvider.Advance(TimeSpan.FromSeconds(5));
        Assert.IsFalse(await coordinator.RefreshForAttendanceFallbackAsync(CancellationToken.None));
        Assert.AreEqual(2, source.CallCount);
    }

    [TestMethod]
    public async Task ConcurrentAttendanceRequests_ShareFailedProviderChain()
    {
        var pending = new TaskCompletionSource<CurrentTimeSample>(TaskCreationOptions.RunContinuationsAsynchronously);
        var source = FakeTimeSource.Calling("primary", _ => pending.Task);
        using var provider = CreateServiceProvider(source);
        using var coordinator = CreateCoordinator(provider, "primary");
        var requests = Enumerable.Range(0, 20)
            .Select(_ => coordinator.RefreshForAttendanceFallbackAsync(CancellationToken.None)).ToArray();
        Assert.AreEqual(1, source.CallCount);
        pending.SetException(new IOException("offline"));
        var results = await Task.WhenAll(requests);
        Assert.IsTrue(results.All(result => !result));
        Assert.AreEqual(1, source.CallCount);
    }

    [TestMethod]
    public async Task RefreshAsync_PropagatesCallerCancellation()
    {
        using var cancellation = new CancellationTokenSource();
        var source = FakeTimeSource.Calling("primary", token =>
        {
            cancellation.Cancel();
            return Task.FromCanceled<CurrentTimeSample>(token);
        });
        using var provider = CreateServiceProvider(source);
        using var coordinator = CreateCoordinator(provider, "primary");

        try
        {
            await coordinator.RefreshAsync(true, cancellation.Token);
            Assert.Fail("Expected caller cancellation to be propagated.");
        }
        catch (OperationCanceledException)
        {
        }
    }

    private static ServiceProvider CreateServiceProvider(params ICurrentTimeSource[] sources)
    {
        var services = new ServiceCollection();
        foreach (var source in sources)
        {
            services.AddSingleton<ICurrentTimeSource>(source);
        }

        return services.BuildServiceProvider();
    }

    private static TimeSampleCoordinator CreateCoordinator(
        IServiceProvider serviceProvider,
        params string[] providerOrder)
    {
        var timeProvider = new FakeTimeProvider(Instant("2026-11-18T12:00:00Z"));
        var clock = new SynchronizedClock(timeProvider, IsraelTimeZone, TimeSpan.FromMinutes(2));

        return CreateCoordinator(serviceProvider, providerOrder, clock, timeProvider);
    }

    private static TimeSampleCoordinator CreateCoordinator(
        IServiceProvider serviceProvider,
        string[] providerOrder,
        SynchronizedClock clock,
        TimeProvider timeProvider)
    {
        var options = Options.Create(new TimeSynchronizationOptions
        {
            ProviderOrder = providerOrder,
            ProviderTimeout = TimeSpan.FromSeconds(5)
        });

        return new TimeSampleCoordinator(
            serviceProvider.GetRequiredService<IServiceScopeFactory>(),
            clock,
            timeProvider,
            options,
            NullLogger<TimeSampleCoordinator>.Instance);
    }

    private static CurrentTimeSample CreateSample(string provider = "test-provider")
    {
        var utcTime = Instant("2026-11-18T09:00:00Z");
        var israelTime = TimeZoneInfo.ConvertTime(utcTime, IsraelTimeZone);

        return new CurrentTimeSample(
            provider,
            IsraelTimeZone.Id,
            israelTime,
            utcTime,
            israelTime.ToString("zzz", CultureInfo.InvariantCulture),
            IsraelTimeZone.IsDaylightSavingTime(utcTime),
            10);
    }

    private static DateTimeOffset Instant(string value) =>
        DateTimeOffset.Parse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal);

    private sealed class FakeTimeSource(
        string id,
        Func<CancellationToken, Task<CurrentTimeSample>> request) : ICurrentTimeSource
    {
        public string Id => id;

        public int CallCount { get; private set; }

        public Task<CurrentTimeSample> GetCurrentTimeAsync(string timeZoneId, CancellationToken cancellationToken)
        {
            CallCount++;
            return request(cancellationToken);
        }

        public static FakeTimeSource Returning(string id, CurrentTimeSample sample) =>
            Calling(id, _ => Task.FromResult(sample));

        public static FakeTimeSource Failing(string id, Exception exception) =>
            Calling(id, _ => Task.FromException<CurrentTimeSample>(exception));

        public static FakeTimeSource Calling(
            string id,
            Func<CancellationToken, Task<CurrentTimeSample>> request) =>
            new(id, request);
    }
}
