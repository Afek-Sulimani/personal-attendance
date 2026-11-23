using Microsoft.VisualStudio.TestTools.UnitTesting;
using Personal.Attendance.Domain.Entities;
using Personal.Attendance.Domain.Enums;

namespace Personal.Attendance.Tests.Domain;

[TestClass]
public sealed class EmployeeProfileCompensationTests
{
    private static readonly DateTimeOffset CreatedAtUtc = DateTimeOffset.Parse("2026-11-20T08:00:00Z");

    [TestMethod]
    public void UpdateCompensation_StoresHourlyRateWithoutCurrency()
    {
        var profile = CreateProfile();

        profile.UpdateCompensation(EmployeeCompensationType.Hourly, 42.75m, CreatedAtUtc.AddHours(1));

        Assert.AreEqual(EmployeeCompensationType.Hourly, profile.CompensationType);
        Assert.AreEqual(42.75m, profile.HourlyRate);
        Assert.AreEqual(CreatedAtUtc.AddHours(1), profile.UpdatedAtUtc);
    }

    [TestMethod]
    public void UpdateCompensation_GlobalTypeClearsHourlyRate()
    {
        var profile = CreateProfile();
        profile.UpdateCompensation(EmployeeCompensationType.Hourly, 42.75m, CreatedAtUtc.AddHours(1));

        profile.UpdateCompensation(EmployeeCompensationType.Global, null, CreatedAtUtc.AddHours(2));

        Assert.AreEqual(EmployeeCompensationType.Global, profile.CompensationType);
        Assert.IsNull(profile.HourlyRate);
    }

    [TestMethod]
    public void UpdateCompensation_RejectsInvalidHourlyRates()
    {
        var profile = CreateProfile();

        foreach (var rate in new[] { 0m, -1m, 12.345m })
        {
            AssertThrows<ArgumentOutOfRangeException>(() =>
                profile.UpdateCompensation(EmployeeCompensationType.Hourly, rate, CreatedAtUtc.AddHours(1)));
        }
    }

    [TestMethod]
    public void UpdateCompensation_GlobalTypeRejectsAnHourlyRate()
    {
        var profile = CreateProfile();

        AssertThrows<ArgumentException>(() =>
            profile.UpdateCompensation(EmployeeCompensationType.Global, 42m, CreatedAtUtc.AddHours(1)));
    }

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

    private static EmployeeProfile CreateProfile() => EmployeeProfile.Create(
        Guid.NewGuid(),
        Guid.NewGuid(),
        "Test Employee",
        "00001",
        CreatedAtUtc);
}
