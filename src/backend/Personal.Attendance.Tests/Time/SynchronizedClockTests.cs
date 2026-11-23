using System.Globalization;
using Microsoft.Extensions.Time.Testing;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Personal.Attendance.Application.Time;
using Personal.Attendance.Infrastructure.Time;

namespace Personal.Attendance.Tests.Time;

[TestClass]
public sealed class SynchronizedClockTests
{
    private static readonly TimeZoneInfo IsraelTimeZone =
        TimeZoneInfo.FindSystemTimeZoneById("Asia/Jerusalem");

    [TestMethod]
    public void TryGetCurrentTime_ReturnsFalseBeforeFirstExternalSample()
    {
        var timeProvider = new FakeTimeProvider(Instant("2026-11-18T12:00:00Z"));
        var clock = CreateClock(timeProvider);

        var hasReading = clock.TryGetCurrentTime(out var reading);

        Assert.IsFalse(hasReading);
        Assert.IsNull(reading);
    }

    [TestMethod]
    public void TryGetCurrentTime_AdvancesFromExternalSampleUsingMonotonicElapsedTime()
    {
        var timeProvider = new FakeTimeProvider(Instant("2026-11-18T12:00:00Z"));
        var clock = CreateClock(timeProvider);
        var sample = CreateSample(Instant("2026-11-18T09:00:00Z"), 80);

        clock.Publish(sample);
        timeProvider.Advance(TimeSpan.FromSeconds(3.5));

        var hasReading = clock.TryGetCurrentTime(out var reading);

        Assert.IsTrue(hasReading);
        Assert.IsNotNull(reading);
        Assert.AreEqual(sample.UtcTime.AddMilliseconds(3_540), reading.UtcTime);
        Assert.AreEqual(TimeZoneInfo.ConvertTime(reading.UtcTime, IsraelTimeZone), reading.LocalTime);
        Assert.AreEqual(3_500, reading.SampleAgeMilliseconds, 0.01);
        Assert.AreEqual(40, reading.LatencyCompensationMilliseconds, 0.01);
    }

    [TestMethod]
    public void TryGetCurrentTime_ReturnsFalseAfterSampleExpires()
    {
        var timeProvider = new FakeTimeProvider(Instant("2026-11-18T12:00:00Z"));
        var clock = CreateClock(timeProvider, TimeSpan.FromSeconds(10));
        clock.Publish(CreateSample(Instant("2026-11-18T09:00:00Z")));
        timeProvider.Advance(TimeSpan.FromSeconds(10.001));

        var hasReading = clock.TryGetCurrentTime(out var reading);

        Assert.IsFalse(hasReading);
        Assert.IsNull(reading);
    }

    [TestMethod]
    [DataRow("2026-01-15T12:00:00Z", 2)]
    [DataRow("2026-07-15T12:00:00Z", 3)]
    public void TryGetCurrentTime_UsesIsraelWinterAndSummerOffsets(
        string utcText,
        int expectedOffsetHours)
    {
        var utcTime = Instant(utcText);
        var timeProvider = new FakeTimeProvider(utcTime);
        var clock = CreateClock(timeProvider);
        clock.Publish(CreateSample(utcTime));

        var hasReading = clock.TryGetCurrentTime(out var reading);

        Assert.IsTrue(hasReading);
        Assert.IsNotNull(reading);
        Assert.AreEqual(TimeSpan.FromHours(expectedOffsetHours), reading.LocalTime.Offset);
    }

    [TestMethod]
    public void Publish_KeepsIndependentSamplesForEachTimeZone()
    {
        var timeProvider = new FakeTimeProvider(Instant("2026-11-18T12:00:00Z"));
        var clock = CreateClock(timeProvider);
        var validSample = CreateSample(Instant("2026-11-18T09:00:00Z"));
        var utcSample = validSample with
        {
            TimeZone = "UTC",
            LocalTime = validSample.UtcTime,
            UtcOffset = "+00:00",
            IsDaylightSaving = false
        };

        clock.Publish(validSample);
        clock.Publish(utcSample);

        Assert.IsTrue(clock.TryGetCurrentTime("Asia/Jerusalem", out var israelReading));
        Assert.IsTrue(clock.TryGetCurrentTime("UTC", out var utcReading));
        Assert.AreEqual("Asia/Jerusalem", israelReading!.TimeZone);
        Assert.AreEqual("UTC", utcReading!.TimeZone);
    }

    [TestMethod]
    public void Publish_RejectsNonUtcTimestampOffset()
    {
        var timeProvider = new FakeTimeProvider(Instant("2026-11-18T12:00:00Z"));
        var clock = CreateClock(timeProvider);
        var validSample = CreateSample(Instant("2026-11-18T09:00:00Z"));
        var invalidSample = validSample with
        {
            UtcTime = validSample.UtcTime.ToOffset(TimeSpan.FromHours(1))
        };

        AssertThrows<InvalidDataException>(() => clock.Publish(invalidSample));
    }

    [TestMethod]
    public void Publish_RejectsInvalidRequestDuration()
    {
        var timeProvider = new FakeTimeProvider(Instant("2026-11-18T12:00:00Z"));
        var clock = CreateClock(timeProvider);
        var invalidSample = CreateSample(Instant("2026-11-18T09:00:00Z"), double.NaN);

        AssertThrows<InvalidDataException>(() => clock.Publish(invalidSample));
    }

    private static SynchronizedClock CreateClock(
        TimeProvider timeProvider,
        TimeSpan? maximumAge = null) =>
        new(timeProvider, IsraelTimeZone, maximumAge ?? TimeSpan.FromMinutes(2));

    private static CurrentTimeSample CreateSample(
        DateTimeOffset utcTime,
        double requestDurationMilliseconds = 0)
    {
        var israelTime = TimeZoneInfo.ConvertTime(utcTime, IsraelTimeZone);

        return new CurrentTimeSample(
            "test-provider",
            IsraelTimeZone.Id,
            israelTime,
            utcTime,
            israelTime.ToString("zzz", CultureInfo.InvariantCulture),
            IsraelTimeZone.IsDaylightSavingTime(utcTime),
            requestDurationMilliseconds);
    }

    private static DateTimeOffset Instant(string value) =>
        DateTimeOffset.Parse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal);

    private static void AssertThrows<TException>(Action action)
        where TException : Exception
    {
        try
        {
            action();
        }
        catch (TException)
        {
            return;
        }

        Assert.Fail($"Expected {typeof(TException).Name} to be thrown.");
    }
}
