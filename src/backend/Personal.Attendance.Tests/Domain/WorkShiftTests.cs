using System.Globalization;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Personal.Attendance.Domain.Entities;

namespace Personal.Attendance.Tests.Domain;

[TestClass]
public sealed class WorkShiftTests
{
    [TestMethod]
    public void ClockOut_ClosesTheSameShiftAndKeepsBothExternalSources()
    {
        var shift = WorkShift.ClockIn(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Instant("2026-11-19T08:00:00Z"),
            "timeapi-io",
            Instant("2026-11-19T07:59:59Z"));

        shift.ClockOut(
            Instant("2026-11-19T16:30:59Z"),
            "timezone-io",
            Instant("2026-11-19T16:29:59Z"));

        Assert.IsFalse(shift.IsOpen);
        Assert.AreEqual("timeapi-io", shift.ClockInProvider);
        Assert.AreEqual("timezone-io", shift.ClockOutProvider);
        Assert.AreEqual(Instant("2026-11-19T16:30:00Z"), shift.ClockOutAtUtc);
    }

    [TestMethod]
    public void ClockIn_TruncatesSecondsDownToTheMinuteWithoutRounding()
    {
        var shift = WorkShift.ClockIn(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Instant("2026-11-19T08:00:59Z"),
            "timeapi-io",
            Instant("2026-11-19T08:00:58Z"));

        Assert.AreEqual(Instant("2026-11-19T08:00:00Z"), shift.ClockInAtUtc);
    }

    [TestMethod]
    public void ClockOut_RejectsAnInstantBeforeClockIn()
    {
        var shift = WorkShift.ClockIn(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Instant("2026-11-19T08:00:00Z"),
            "timeapi-io",
            Instant("2026-11-19T07:59:59Z"));

        AssertThrows<ArgumentOutOfRangeException>(() => shift.ClockOut(
            Instant("2026-11-19T07:59:00Z"),
            "timeapi-io",
            Instant("2026-11-19T07:58:59Z")));
        Assert.IsTrue(shift.IsOpen);
    }

    [TestMethod]
    public void ClockOut_RejectsASecondClockOut()
    {
        var shift = WorkShift.ClockIn(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Instant("2026-11-19T08:00:00Z"),
            "timeapi-io",
            Instant("2026-11-19T07:59:59Z"));
        shift.ClockOut(Instant("2026-11-19T16:00:00Z"), "timeapi-io", Instant("2026-11-19T15:59:59Z"));

        AssertThrows<InvalidOperationException>(() => shift.ClockOut(
            Instant("2026-11-19T16:01:00Z"),
            "timeapi-io",
            Instant("2026-11-19T16:00:59Z")));
    }

    private static DateTimeOffset Instant(string value) => DateTimeOffset.Parse(value, CultureInfo.InvariantCulture);

    private static void AssertThrows<TException>(Action action) where TException : Exception
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
