using Microsoft.VisualStudio.TestTools.UnitTesting;
using Personal.Attendance.Infrastructure.Attendance;

namespace Personal.Attendance.Tests.Attendance;

[TestClass]
public sealed class CorrectionAttentionPolicyTests
{
    private static readonly TimeZoneInfo Israel = TimeZoneInfo.FindSystemTimeZoneById("Asia/Jerusalem");

    [TestMethod]
    public void PreviousPeriodRequestNeedsAttentionOnFirstDayOfNewPeriod()
    {
        var nowUtc = new DateTimeOffset(2026, 11, 16, 8, 0, 0, TimeSpan.Zero);
        Assert.IsTrue(CorrectionAttentionPolicy.IsAttentionRequired(new DateOnly(2026, 11, 15), nowUtc, 16, Israel));
        Assert.IsFalse(CorrectionAttentionPolicy.IsAttentionRequired(new DateOnly(2026, 11, 16), nowUtc, 16, Israel));
    }

    [TestMethod]
    public void CurrentPeriodRequestDoesNotNeedAttentionBeforeCutoff()
    {
        var nowUtc = new DateTimeOffset(2026, 11, 15, 8, 0, 0, TimeSpan.Zero);
        Assert.IsFalse(CorrectionAttentionPolicy.IsAttentionRequired(new DateOnly(2026, 11, 15), nowUtc, 16, Israel));
    }

    [TestMethod]
    public void FebruaryStartDayIsClampedToLastCalendarDay()
    {
        var nowUtc = new DateTimeOffset(2027, 2, 28, 12, 0, 0, TimeSpan.Zero);
        Assert.IsTrue(CorrectionAttentionPolicy.IsAttentionRequired(new DateOnly(2027, 2, 27), nowUtc, 31, Israel));
    }
}
