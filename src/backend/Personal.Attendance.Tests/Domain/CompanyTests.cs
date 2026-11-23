using System.Globalization;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Personal.Attendance.Domain.Entities;

namespace Personal.Attendance.Tests.Domain;

[TestClass]
public sealed class CompanyTests
{
    [TestMethod]
    public void Create_NormalizesCompanyCodeForLookup()
    {
        var company = Company.Create("  personal-test ", "Personal Test", Instant("2026-11-19T12:00:00Z"));

        Assert.AreEqual("personal-test", company.CompanyCode);
        Assert.AreEqual("PERSONAL-TEST", company.NormalizedCompanyCode);
    }

    private static DateTimeOffset Instant(string value) => DateTimeOffset.Parse(value, CultureInfo.InvariantCulture);
}
