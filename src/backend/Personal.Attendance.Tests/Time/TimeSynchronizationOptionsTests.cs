using Microsoft.VisualStudio.TestTools.UnitTesting;
using Personal.Attendance.Api.Time;

namespace Personal.Attendance.Tests.Time;

[TestClass]
public sealed class TimeSynchronizationOptionsTests
{
    [TestMethod]
    public void Defaults_RefreshHourlyAndExpireSampleAfterFiveMinuteGracePeriod()
    {
        var options = new TimeSynchronizationOptions();

        Assert.AreEqual(TimeSpan.FromHours(1), options.RefreshInterval);
        Assert.AreEqual(TimeSpan.FromMinutes(65), options.MaximumSampleAge);
        Assert.AreEqual(TimeSpan.FromSeconds(5), options.ProviderTimeout);
        Assert.AreEqual(TimeSpan.FromMinutes(5), options.RefreshFailureCooldown);
    }
}
