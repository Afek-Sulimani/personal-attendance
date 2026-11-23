using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Personal.Attendance.Domain.Entities;
using Personal.Attendance.Infrastructure.Persistence;

namespace Personal.Attendance.Tests.Persistence;

[TestClass]
public sealed class PersonalAttendanceModelTests
{
    [TestMethod]
    public void Model_MapsIdentityAndProductTablesToPersonalNames()
    {
        var options = new DbContextOptionsBuilder<PersonalAttendanceDbContext>()
            .UseSqlServer("Server=localhost;Database=PersonalModelOnly;Trusted_Connection=True;TrustServerCertificate=True")
            .Options;
        using var context = new PersonalAttendanceDbContext(options);

        var tableNames = context.Model.GetEntityTypes()
            .Select(entity => entity.GetTableName())
            .Where(name => name is not null)
            .ToHashSet(StringComparer.Ordinal);

        Assert.IsTrue(tableNames.Contains("ATTENDANCE_USERS"));
        Assert.IsTrue(tableNames.Contains("ATTENDANCE_USER_ROLES"));
        Assert.IsTrue(tableNames.Contains("ATTENDANCE_COMPANIES"));
        Assert.IsTrue(tableNames.Contains("ATTENDANCE_EMPLOYEE_INVITATIONS"));
        Assert.IsTrue(tableNames.Contains("ATTENDANCE_WORK_SHIFTS"));
        Assert.IsTrue(tableNames.All(name => name!.StartsWith("ATTENDANCE_", StringComparison.Ordinal)));
    }

    [TestMethod]
    public void Model_EnforcesOneOpenShiftPerEmployeeAndCompany()
    {
        var options = new DbContextOptionsBuilder<PersonalAttendanceDbContext>()
            .UseSqlServer("Server=localhost;Database=PersonalModelOnly;Trusted_Connection=True;TrustServerCertificate=True")
            .Options;
        using var context = new PersonalAttendanceDbContext(options);
        var entity = context.Model.FindEntityType(typeof(WorkShift));
        Assert.IsNotNull(entity);

        var openShiftIndex = entity.GetIndexes().Single(index => index.GetFilter() == "[ClockOutAtUtc] IS NULL");

        Assert.IsTrue(entity.GetIndexes().Single(index => index.Properties.Select(p => p.Name)
            .SequenceEqual(new[] { "CompanyId", "EmployeeProfileId", "WorkDate" })).IsUnique);
        Assert.IsTrue(openShiftIndex.IsUnique);
        CollectionAssert.AreEqual(
            new[] { nameof(WorkShift.CompanyId), nameof(WorkShift.EmployeeProfileId) },
            openShiftIndex.Properties.Select(property => property.Name).ToArray());
    }

    [TestMethod]
    public void Model_LeavesDeferredLockoutFieldsOutOfTheUserTable()
    {
        var options = new DbContextOptionsBuilder<PersonalAttendanceDbContext>()
            .UseSqlServer("Server=localhost;Database=PersonalModelOnly;Trusted_Connection=True;TrustServerCertificate=True")
            .Options;
        using var context = new PersonalAttendanceDbContext(options);
        var user = context.Model.FindEntityType(typeof(PersonalUser));
        Assert.IsNotNull(user);

        Assert.IsNull(user.FindProperty(nameof(PersonalUser.LockoutEnabled)));
        Assert.IsNull(user.FindProperty(nameof(PersonalUser.LockoutEnd)));
        Assert.IsNull(user.FindProperty(nameof(PersonalUser.AccessFailedCount)));
        Assert.IsTrue(user.GetIndexes().Any(index =>
            index.IsUnique && index.Properties.Any(property => property.Name == nameof(PersonalUser.NormalizedEmail))));
    }

    [TestMethod]
    public void Model_UsesRowVersionForConcurrentShiftUpdates()
    {
        var options = new DbContextOptionsBuilder<PersonalAttendanceDbContext>()
            .UseSqlServer("Server=localhost;Database=PersonalModelOnly;Trusted_Connection=True;TrustServerCertificate=True")
            .Options;
        using var context = new PersonalAttendanceDbContext(options);

        var shiftVersion = context.Model.FindEntityType(typeof(WorkShift))?
            .FindProperty(nameof(WorkShift.RowVersion));
        Assert.IsNotNull(shiftVersion);
        Assert.IsTrue(shiftVersion.IsConcurrencyToken);
    }

    [TestMethod]
    public void Model_StoresCompanySpecificCompensationWithSqlConstraint()
    {
        var options = new DbContextOptionsBuilder<PersonalAttendanceDbContext>()
            .UseSqlServer("Server=localhost;Database=PersonalModelOnly;Trusted_Connection=True;TrustServerCertificate=True")
            .Options;
        using var context = new PersonalAttendanceDbContext(options);
        var profile = context.GetService<IDesignTimeModel>().Model.FindEntityType(typeof(EmployeeProfile));
        Assert.IsNotNull(profile);

        Assert.IsTrue(profile.FindProperty(nameof(EmployeeProfile.CompensationType))!.IsNullable);
        Assert.AreEqual("decimal(18,2)", profile.FindProperty(nameof(EmployeeProfile.HourlyRate))!.GetColumnType());
        var constraint = profile.GetCheckConstraints().Single(item => item.Name == "CK_ATTENDANCE_EMPLOYEE_PROFILES_COMPENSATION");
        StringAssert.Contains(constraint.Sql!, "[HourlyRate] IS NOT NULL");
        StringAssert.Contains(constraint.Sql!, "[HourlyRate] > 0");
    }

    [TestMethod]
    public void Model_UsesUniqueTokenHashAndRowVersionForInvitationAcceptance()
    {
        var options = new DbContextOptionsBuilder<PersonalAttendanceDbContext>()
            .UseSqlServer("Server=localhost;Database=PersonalModelOnly;Trusted_Connection=True;TrustServerCertificate=True")
            .Options;
        using var context = new PersonalAttendanceDbContext(options);
        var invitation = context.Model.FindEntityType(typeof(EmployeeInvitation));
        Assert.IsNotNull(invitation);

        var tokenHashIndex = invitation.GetIndexes().Single(index =>
            index.Properties.Any(property => property.Name == nameof(EmployeeInvitation.TokenHash)));
        var rowVersion = invitation.FindProperty(nameof(EmployeeInvitation.RowVersion));

        Assert.IsTrue(tokenHashIndex.IsUnique);
        Assert.IsNotNull(rowVersion);
        Assert.IsTrue(rowVersion.IsConcurrencyToken);
    }
}
