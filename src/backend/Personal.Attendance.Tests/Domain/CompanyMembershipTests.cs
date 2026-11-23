using System.Globalization;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Personal.Attendance.Domain.Entities;
using Personal.Attendance.Domain.Enums;

namespace Personal.Attendance.Tests.Domain;

[TestClass]
public sealed class CompanyMembershipTests
{
    [TestMethod]
    public void AssignRole_AllowsMultipleRolesAndDoesNotDuplicateOneRole()
    {
        var membership = CompanyMembership.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            null,
            Instant("2026-11-19T12:00:00Z"));

        membership.AssignRole(CompanyRole.Hr);
        membership.AssignRole(CompanyRole.TeamLead);
        membership.AssignRole(CompanyRole.Hr);

        CollectionAssert.AreEquivalent(
            new[] { CompanyRole.Hr, CompanyRole.TeamLead },
            membership.Roles.Select(role => role.Role).ToArray());
    }

    private static DateTimeOffset Instant(string value) =>
        DateTimeOffset.Parse(value, CultureInfo.InvariantCulture);
}
