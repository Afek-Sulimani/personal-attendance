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
public sealed class BackgroundRecoveryTests
{
    [TestMethod]
    public async Task ProviderFailure_RetriesAfterFiveMinutesWithoutPunchRequests()
    {
        var time = new FakeTimeProvider();
        var options = Options.Create(new TimeSynchronizationOptions { ProviderOrder = ["recovering"] });
        var source = new RecoveringSource(time);
        using var services = new ServiceCollection().AddSingleton<ICurrentTimeSource>(source).BuildServiceProvider();
        var registry = new CompanyTimeZoneRegistry(options);
        var clock = new SynchronizedClock(time, TimeZoneInfo.FindSystemTimeZoneById("Asia/Jerusalem"), TimeSpan.FromMinutes(65));
        using var coordinator = new TimeSampleCoordinator(services.GetRequiredService<IServiceScopeFactory>(), clock, time, options, NullLogger<TimeSampleCoordinator>.Instance);
        using var background = new TimeSynchronizationService(coordinator, registry, time, options, NullLogger<TimeSynchronizationService>.Instance);
        await background.StartAsync(CancellationToken.None);
        await WaitForAsync(() => source.Count == 1);
        // Allow the initial refresh to finish and the periodic timer to be registered.
        await Task.Delay(50);
        time.Advance(TimeSpan.FromMinutes(5));
        await WaitForAsync(() => source.Count == 2);
        Assert.IsTrue(clock.TryGetCurrentTime("Asia/Jerusalem", out _));
        Assert.IsFalse(coordinator.NeedsRecovery("Asia/Jerusalem"));
        time.Advance(TimeSpan.FromMinutes(5));
        await Task.Delay(50);
        Assert.AreEqual(2, source.Count, "Healthy zones should not call the provider every five minutes.");
        await background.StopAsync(CancellationToken.None);
    }

    private static async Task WaitForAsync(Func<bool> condition)
    {
        for (var i = 0; i < 100 && !condition(); i++) await Task.Delay(10);
        Assert.IsTrue(condition());
    }

    private sealed class RecoveringSource(FakeTimeProvider time) : ICurrentTimeSource
    {
        public string Id => "recovering";
        public int Count;
        public Task<CurrentTimeSample> GetCurrentTimeAsync(string timeZoneId, CancellationToken cancellationToken)
        {
            if (Interlocked.Increment(ref Count) == 1) throw new HttpRequestException("Both configured sources unavailable");
            var utc = time.GetUtcNow();
            var zone = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
            var local = TimeZoneInfo.ConvertTime(utc, zone);
            return Task.FromResult(new CurrentTimeSample(Id, zone.Id, local, utc, local.ToString("zzz"), zone.IsDaylightSavingTime(utc), 0));
        }
    }
}
