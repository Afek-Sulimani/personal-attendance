using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Time.Testing;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Personal.Attendance.Application.Attendance;
using Personal.Attendance.Application.Time;
using Personal.Attendance.Domain.Entities;
using Personal.Attendance.Domain.Enums;
using Personal.Attendance.Infrastructure.Attendance;
using Personal.Attendance.Infrastructure.Persistence;

namespace Personal.Attendance.Tests.Attendance;

[TestClass]
public sealed class MonthlyAttendanceReportServiceTests
{
    private static readonly DateTimeOffset CreatedAtUtc = Instant("2026-01-02T08:00:00Z");
    private static readonly DateTimeOffset CurrentTimeUtc = Instant("2026-11-20T10:00:00Z");
    private static readonly DateOnly PeriodStart = new(2026, 10, 16);
    private static readonly DateOnly PeriodEnd = new(2026, 11, 15);

    [TestMethod]
    public async Task GetCurrentMineAsync_ReturnsConfiguredPayrollPeriodAndMinuteTotals()
    {
        await using var setup = await CreateSetupAsync();
        setup.DbContext.WorkShifts.Add(CreateClosedShift(setup.EmployeeProfile, new DateOnly(2026, 11, 19)));
        await setup.DbContext.SaveChangesAsync();
        var service = CreateService(setup.DbContext);

        var report = await service.GetCurrentMineAsync(setup.EmployeeActor, CancellationToken.None);

        Assert.AreEqual(new DateOnly(2026, 11, 16), report.PeriodStart);
        Assert.AreEqual(new DateOnly(2026, 12, 15), report.PeriodEnd);
        Assert.AreEqual("Asia/Jerusalem", report.TimeZoneId);
        Assert.AreEqual(4, report.ScheduledWorkdayCount);
        Assert.AreEqual(1, report.CompletedShiftCount);
        Assert.AreEqual(0, report.MissingWorkdayCount);
        Assert.AreEqual(480, report.TotalWorkedMinutes);
    }

    [TestMethod]
    public async Task GetMineAsync_AssignsAllOvernightMinutesToClockInPayrollPeriod()
    {
        await using var setup = await CreateSetupAsync();
        setup.PayrollSettings.Update(16, "Asia/Jerusalem", new TimeOnly(9, 0), new TimeOnly(17, 0),
            [DayOfWeek.Thursday, DayOfWeek.Saturday, DayOfWeek.Sunday], setup.ManagerMembership.MembershipId, CurrentTimeUtc);
        setup.DbContext.WorkShifts.Add(CreateOvernightBoundaryShift(setup.EmployeeProfile));
        await setup.DbContext.SaveChangesAsync();
        var service = CreateService(setup.DbContext);

        var earlierPeriod = await service.GetMineAsync(setup.EmployeeActor, 2026, 10, CancellationToken.None);
        var laterPeriod = await service.GetMineAsync(setup.EmployeeActor, 2026, 11, CancellationToken.None);
        var firstDay = earlierPeriod.Days.Single(day => day.Date == new DateOnly(2026, 10, 31));

        Assert.AreEqual(480, earlierPeriod.TotalWorkedMinutes);
        Assert.AreEqual(0, laterPeriod.TotalWorkedMinutes);
        Assert.AreEqual(480, earlierPeriod.TotalWorkedMinutes + laterPeriod.TotalWorkedMinutes);
        Assert.AreEqual("Complete", firstDay.Status);
        Assert.AreEqual(1, firstDay.Shifts.Count);
        Assert.IsFalse(firstDay.Shifts[0].IsContinuation);
        Assert.AreEqual(new DateOnly(2026, 10, 31), firstDay.Shifts[0].WorkDate);
        Assert.AreEqual(480, firstDay.Shifts[0].WorkedMinutes);
    }

    [TestMethod]
    public async Task GetMineAsync_IncludesPendingCorrectionHoursInReportedTotalAndSeparatesThem()
    {
        await using var setup = await CreateSetupAsync();
        var workDate = GetThursdays(PeriodStart, PeriodEnd).First();
        var correction = AttendanceCorrectionRequest.Create(
            setup.Company.Id,
            setup.EmployeeProfile.EmployeeProfileId,
            setup.EmployeeMembership.MembershipId,
            null,
            setup.ManagerMembership.MembershipId,
            workDate,
            "Asia/Jerusalem",
            GetIsraelUtc(workDate, new TimeOnly(9, 0)),
            GetIsraelUtc(workDate, new TimeOnly(17, 30)),
            CurrentTimeUtc,
            "test-provider",
            CurrentTimeUtc.AddSeconds(-1));
        setup.DbContext.AttendanceCorrectionRequests.Add(correction);
        await setup.DbContext.SaveChangesAsync();

        var report = await CreateService(setup.DbContext)
            .GetMineAsync(setup.EmployeeActor, workDate.Year, workDate.Month, CancellationToken.None);
        var day = report.Days.Single(item => item.Date == workDate);

        Assert.AreEqual("PendingApproval", day.Status);
        Assert.IsNotNull(day.PendingCorrection);
        Assert.AreEqual(9, day.PendingCorrection.ClockInAtLocal?.Hour);
        Assert.AreEqual(17, day.PendingCorrection.ClockOutAtLocal?.Hour);
        Assert.AreEqual(510, day.PendingCorrection.WorkedMinutes);
        Assert.AreEqual(510, report.TotalWorkedMinutes);
        Assert.AreEqual(510, report.PendingCorrectionMinutes);
    }

    [TestMethod]
    public async Task GetMineAsync_TotalCombinesRecordedAndPendingHours()
    {
        await using var setup = await CreateSetupAsync();
        var workDates = GetThursdays(PeriodStart, PeriodEnd).Take(2).ToArray();
        setup.DbContext.WorkShifts.Add(CreateClosedShift(setup.EmployeeProfile, workDates[0]));
        var requestedClockInUtc = GetIsraelUtc(workDates[1], new TimeOnly(9, 0));
        setup.DbContext.AttendanceCorrectionRequests.Add(AttendanceCorrectionRequest.Create(
            setup.Company.Id,
            setup.EmployeeProfile.EmployeeProfileId,
            setup.EmployeeMembership.MembershipId,
            null,
            setup.ManagerMembership.MembershipId,
            workDates[1],
            "Asia/Jerusalem",
            requestedClockInUtc,
            requestedClockInUtc.AddMinutes(510),
            CurrentTimeUtc,
            "test-provider",
            CurrentTimeUtc.AddSeconds(-1)));
        await setup.DbContext.SaveChangesAsync();

        var report = await CreateService(setup.DbContext)
            .GetMineAsync(setup.EmployeeActor, PeriodStart.Year, PeriodStart.Month, CancellationToken.None);

        Assert.AreEqual(990, report.TotalWorkedMinutes);
        Assert.AreEqual(510, report.PendingCorrectionMinutes);
    }

    [TestMethod]
    public async Task GetTeamMemberAsync_AllowsDirectManagerAndDeniesUnrelatedManager()
    {
        await using var setup = await CreateSetupAsync();
        await AddClosedWorkdaysAsync(setup, PeriodStart, PeriodEnd);
        var otherManager = await AddMemberAsync(setup, "Other Manager", CompanyRole.TeamLead, null);
        var service = CreateService(setup.DbContext);

        var members = await service.GetTeamMembersAsync(setup.ManagerActor, CancellationToken.None);
        var report = await service.GetTeamMemberAsync(setup.ManagerActor,
            setup.EmployeeMembership.MembershipId, 2026, 10, CancellationToken.None);

        Assert.AreEqual(1, members.Count);
        Assert.AreEqual(setup.EmployeeMembership.MembershipId, members[0].EmployeeMembershipId);
        Assert.AreEqual(1_920, report.TotalWorkedMinutes);
        await AssertThrowsAsync<MonthlyAttendanceReportAccessDeniedException>(() =>
            service.GetTeamMemberAsync(otherManager.Actor,
                setup.EmployeeMembership.MembershipId, 2026, 11, CancellationToken.None));
    }

    private static async Task<TestSetup> CreateSetupAsync(
        CompanyRole employeeRole = CompanyRole.Employee,
        Guid? employeeManagerId = null)
    {
        var options = new DbContextOptionsBuilder<PersonalAttendanceDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var dbContext = new PersonalAttendanceDbContext(options);
        var company = Company.Create("TEST-01", "Test Company", CreatedAtUtc);
        var managerUserId = Guid.NewGuid();
        var managerMembership = CompanyMembership.Create(company.Id, managerUserId, null, CreatedAtUtc);
        managerMembership.AssignRole(CompanyRole.TeamLead);
        var managerProfile = EmployeeProfile.Create(company.Id, managerMembership.MembershipId, "Direct Manager", "00001", CreatedAtUtc);
        var payrollSettings = CompanyPayrollSettings.Create(
            company.Id,
            16,
            "Asia/Jerusalem",
            new TimeOnly(9, 0),
            new TimeOnly(17, 0),
            [DayOfWeek.Thursday],
            managerMembership.MembershipId,
            CreatedAtUtc);
        var employeeUserId = Guid.NewGuid();
        var managerId = employeeManagerId ?? (employeeRole == CompanyRole.Admin ? null : managerMembership.MembershipId);
        var employeeMembership = CompanyMembership.Create(company.Id, employeeUserId, managerId, CreatedAtUtc);
        employeeMembership.AssignRole(employeeRole);
        var employeeProfile = EmployeeProfile.Create(company.Id, employeeMembership.MembershipId, "Test Employee", "00002", CreatedAtUtc);

        dbContext.AddRange(company, managerMembership, managerProfile, payrollSettings, employeeMembership, employeeProfile);
        await dbContext.SaveChangesAsync();
        var employeeActor = new AttendanceActor(employeeUserId, company.Id, employeeMembership.MembershipId, employeeProfile.EmployeeProfileId);
        var managerActor = new AttendanceActor(managerUserId, company.Id, managerMembership.MembershipId, managerProfile.EmployeeProfileId);
        return new TestSetup(dbContext, company, employeeMembership, employeeProfile, employeeActor,
            managerMembership, managerProfile, managerActor, payrollSettings);
    }

    private static async Task<MemberSetup> AddMemberAsync(TestSetup setup, string displayName, CompanyRole role, Guid? managerId)
    {
        var userId = Guid.NewGuid();
        var membership = CompanyMembership.Create(setup.Company.Id, userId, managerId, CreatedAtUtc);
        membership.AssignRole(role);
        var profile = EmployeeProfile.Create(setup.Company.Id, membership.MembershipId, displayName, null, CreatedAtUtc);
        setup.DbContext.AddRange(membership, profile);
        await setup.DbContext.SaveChangesAsync();
        return new MemberSetup(new AttendanceActor(userId, setup.Company.Id, membership.MembershipId, profile.EmployeeProfileId));
    }

    private static async Task AddClosedWorkdaysAsync(TestSetup setup, DateOnly start, DateOnly end)
    {
        foreach (var workDate in GetThursdays(start, end))
        {
            setup.DbContext.WorkShifts.Add(CreateClosedShift(setup.EmployeeProfile, workDate));
        }
        await setup.DbContext.SaveChangesAsync();
    }

    private static IEnumerable<DateOnly> GetThursdays(DateOnly start, DateOnly end)
    {
        for (var date = start; date <= end; date = date.AddDays(1))
        {
            if (date.DayOfWeek == DayOfWeek.Thursday) yield return date;
        }
    }

    private static WorkShift CreateClosedShift(EmployeeProfile profile, DateOnly workDate)
    {
        var zone = TimeZoneInfo.FindSystemTimeZoneById("Asia/Jerusalem");
        var localClockIn = workDate.ToDateTime(new TimeOnly(9, 0), DateTimeKind.Unspecified);
        var clockInUtc = new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(localClockIn, zone), TimeSpan.Zero);
        var clockOutUtc = clockInUtc.AddHours(8);
        var shift = WorkShift.ClockIn(profile.CompanyId, profile.EmployeeProfileId,
            clockInUtc, "test-provider", clockInUtc, "Asia/Jerusalem", workDate);
        shift.ClockOut(clockOutUtc, "test-provider", clockOutUtc, "Asia/Jerusalem");
        return shift;
    }

    private static WorkShift CreateOpenShift(EmployeeProfile profile, DateOnly workDate)
    {
        var clockInUtc = GetIsraelUtc(workDate, new TimeOnly(9, 0));
        return WorkShift.ClockIn(profile.CompanyId, profile.EmployeeProfileId,
            clockInUtc, "test-provider", clockInUtc, "Asia/Jerusalem", workDate);
    }

    private static WorkShift CreateOvernightBoundaryShift(EmployeeProfile profile)
    {
        var zone = TimeZoneInfo.FindSystemTimeZoneById("Asia/Jerusalem");
        var clockInLocal = new DateTime(2026, 10, 31, 20, 0, 0, DateTimeKind.Unspecified);
        var clockOutLocal = new DateTime(2026, 11, 1, 4, 0, 0, DateTimeKind.Unspecified);
        var clockInUtc = new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(clockInLocal, zone), TimeSpan.Zero);
        var clockOutUtc = new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(clockOutLocal, zone), TimeSpan.Zero);
        var shift = WorkShift.ClockIn(profile.CompanyId, profile.EmployeeProfileId,
            clockInUtc, "test-provider", clockInUtc, "Asia/Jerusalem", new DateOnly(2026, 10, 31));
        shift.ClockOut(clockOutUtc, "test-provider", clockOutUtc, "Asia/Jerusalem");
        return shift;
    }

    private static AttendanceCorrectionRequest CreatePendingCorrection(TestSetup setup, DateOnly workDate)
    {
        var requestedClockInUtc = GetIsraelUtc(workDate, new TimeOnly(9, 0));
        return AttendanceCorrectionRequest.Create(
            setup.Company.Id,
            setup.EmployeeProfile.EmployeeProfileId,
            setup.EmployeeMembership.MembershipId,
            null,
            setup.ManagerMembership.MembershipId,
            workDate,
            "Asia/Jerusalem",
            requestedClockInUtc,
            null,
            CurrentTimeUtc,
            "test-provider",
            CurrentTimeUtc.AddSeconds(-1));
    }

    private static DateTimeOffset GetIsraelUtc(DateOnly date, TimeOnly time)
    {
        var zone = TimeZoneInfo.FindSystemTimeZoneById("Asia/Jerusalem");
        var localDateTime = date.ToDateTime(time, DateTimeKind.Unspecified);
        return new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(localDateTime, zone), TimeSpan.Zero);
    }

    private static MonthlyAttendanceReportService CreateService(PersonalAttendanceDbContext dbContext) =>
        new(dbContext, new FixedAttendanceClock(CurrentTimeUtc), new TestCompanyTimeZoneRegistry());

    private static DateTimeOffset Instant(string value) =>
        DateTimeOffset.Parse(value, CultureInfo.InvariantCulture);

    private static async Task AssertThrowsAsync<TException>(Func<Task> action)
        where TException : Exception
    {
        try
        {
            await action();
        }
        catch (TException)
        {
            return;
        }

        Assert.Fail($"Expected {typeof(TException).Name} to be thrown.");
    }

    private sealed class TestSetup(
        PersonalAttendanceDbContext dbContext,
        Company company,
        CompanyMembership employeeMembership,
        EmployeeProfile employeeProfile,
        AttendanceActor employeeActor,
        CompanyMembership managerMembership,
        EmployeeProfile managerProfile,
        AttendanceActor managerActor,
        CompanyPayrollSettings payrollSettings) : IAsyncDisposable
    {
        public PersonalAttendanceDbContext DbContext { get; } = dbContext;
        public Company Company { get; } = company;
        public CompanyMembership EmployeeMembership { get; } = employeeMembership;
        public EmployeeProfile EmployeeProfile { get; } = employeeProfile;
        public AttendanceActor EmployeeActor { get; } = employeeActor;
        public CompanyMembership ManagerMembership { get; } = managerMembership;
        public EmployeeProfile ManagerProfile { get; } = managerProfile;
        public AttendanceActor ManagerActor { get; } = managerActor;
        public CompanyPayrollSettings PayrollSettings { get; } = payrollSettings;

        public ValueTask DisposeAsync() => DbContext.DisposeAsync();
    }

    private sealed record MemberSetup(AttendanceActor Actor);

    private sealed class TestCompanyTimeZoneRegistry : ICompanyTimeZoneRegistry
    {
        public string DefaultTimeZoneId => "Asia/Jerusalem";
        public string Register(string timeZoneId) => TimeZoneInfo.FindSystemTimeZoneById(timeZoneId).Id;
        public IReadOnlyList<string> GetRegisteredTimeZones() => [DefaultTimeZoneId, "UTC"];
    }

    private sealed class FixedAttendanceClock(DateTimeOffset utcTime) : ICurrentAttendanceClock
    {
        public Task<ClockReading> GetCurrentTimeAsync(string timeZoneId, CancellationToken cancellationToken) =>
            Task.FromResult(CreateReading(timeZoneId));

        public Task<ClockReading> GetCurrentTimeForAttendanceAsync(string timeZoneId, CancellationToken cancellationToken) =>
            Task.FromResult(CreateReading(timeZoneId));

        private ClockReading CreateReading(string timeZoneId)
        {
            var zone = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
            return new ClockReading(
                zone.Id,
                "test-provider",
                utcTime,
                TimeZoneInfo.ConvertTime(utcTime, zone),
                utcTime,
                0,
                0,
                0);
        }
    }
}
