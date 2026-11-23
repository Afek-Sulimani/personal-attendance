using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Personal.Attendance.Application.Attendance;
using Personal.Attendance.Application.Time;
using Personal.Attendance.Domain.Entities;
using Personal.Attendance.Domain.Enums;
using Personal.Attendance.Infrastructure.Attendance;
using Personal.Attendance.Infrastructure.Persistence;

namespace Personal.Attendance.Tests.Attendance;

[TestClass]
public sealed class AttendanceServiceTests
{
    private static readonly DateTimeOffset CreatedAtUtc = Instant("2026-01-02T08:00:00Z");

    [TestMethod]
    public async Task ClockInAsync_SavesMinutePrecisionAndProviderProvenance()
    {
        await using var setup = await CreateEmployeeAsync();
        var clock = new FakeIsraelClock(Reading("2026-11-19T08:05:42Z", "timeapi-io"));
        var service = CreateService(setup.DbContext, clock);

        var result = await service.ClockInAsync(setup.Actor, CancellationToken.None);
        var savedShift = await setup.DbContext.WorkShifts.SingleAsync();

        Assert.AreEqual("ClockedIn", result.Status);
        Assert.AreEqual(Instant("2026-11-19T08:05:00Z"), result.OccurredAtUtc);
        Assert.AreEqual(Instant("2026-11-19T09:05:00+01:00"), result.OccurredAtLocal);
        Assert.AreEqual("Asia/Jerusalem", result.TimeZone);
        Assert.AreEqual("timeapi-io", result.Provider);
        Assert.AreEqual(Instant("2026-11-19T08:05:40Z"), result.ProviderSampleUtc);
        Assert.AreEqual(result.WorkShiftId, savedShift.WorkShiftId);
        Assert.AreEqual("timeapi-io", savedShift.ClockInProvider);
        Assert.AreEqual(Instant("2026-11-19T08:05:00Z"), savedShift.ClockInAtUtc);
        Assert.AreEqual(1, clock.Calls);
    }

    [TestMethod]
    public async Task ClockOutAsync_ClosesExistingShiftUsingIsraelReading()
    {
        await using var setup = await CreateEmployeeAsync();
        var shift = WorkShift.ClockIn(
            setup.Actor.CompanyId,
            setup.Actor.EmployeeProfileId,
            Instant("2026-11-19T08:05:00Z"),
            "timeapi-io",
            Instant("2026-11-19T08:04:59Z"));
        setup.DbContext.WorkShifts.Add(shift);
        await setup.DbContext.SaveChangesAsync();

        var clock = new FakeIsraelClock(Reading("2026-11-19T16:30:59Z", "timezone-io"));
        var service = CreateService(setup.DbContext, clock);

        var result = await service.ClockOutAsync(setup.Actor, CancellationToken.None);
        var savedShift = await setup.DbContext.WorkShifts.SingleAsync();

        Assert.AreEqual("ClockedOut", result.Status);
        Assert.AreEqual(shift.WorkShiftId, result.WorkShiftId);
        Assert.AreEqual(Instant("2026-11-19T16:30:00Z"), result.OccurredAtUtc);
        Assert.AreEqual(Instant("2026-11-19T17:30:00+01:00"), result.OccurredAtLocal);
        Assert.AreEqual("timezone-io", savedShift.ClockOutProvider);
        Assert.AreEqual(Instant("2026-11-19T16:30:00Z"), savedShift.ClockOutAtUtc);
        Assert.IsFalse(savedShift.IsOpen);
    }

    [TestMethod]
    public async Task ClockInAsync_RejectsSecondOpenShiftWithoutRequestingTime()
    {
        await using var setup = await CreateEmployeeAsync();
        setup.DbContext.WorkShifts.Add(CreateOpenShift(setup.Actor));
        await setup.DbContext.SaveChangesAsync();
        var clock = new FakeIsraelClock();
        var service = CreateService(setup.DbContext, clock);

        await AssertThrowsAsync<AttendanceConflictException>(() =>
            service.ClockInAsync(setup.Actor, CancellationToken.None));

        Assert.AreEqual(0, clock.Calls);
        Assert.AreEqual(1, await setup.DbContext.WorkShifts.CountAsync());
    }

    [TestMethod]
    public async Task ClockOutAsync_RejectsWhenNoShiftIsOpenWithoutRequestingTime()
    {
        await using var setup = await CreateEmployeeAsync();
        var clock = new FakeIsraelClock();
        var service = CreateService(setup.DbContext, clock);

        await AssertThrowsAsync<AttendanceConflictException>(() =>
            service.ClockOutAsync(setup.Actor, CancellationToken.None));

        Assert.AreEqual(0, clock.Calls);
    }

    [TestMethod]
    public async Task GetCurrentAsync_ReturnsTheOpenShiftForTheAuthenticatedEmployee()
    {
        await using var setup = await CreateEmployeeAsync();
        var shift = CreateOpenShift(setup.Actor);
        setup.DbContext.WorkShifts.Add(shift);
        await setup.DbContext.SaveChangesAsync();
        var service = CreateService(setup.DbContext, new FakeIsraelClock());

        var result = await service.GetCurrentAsync(setup.Actor, CancellationToken.None);

        Assert.IsTrue(result.HasOpenShift);
        Assert.IsNotNull(result.OpenShift);
        Assert.AreEqual(shift.WorkShiftId, result.OpenShift.WorkShiftId);
        Assert.AreEqual(Instant("2026-11-19T09:05:00+01:00"), result.OpenShift.ClockInAtLocal);
    }

    [TestMethod]
    public async Task ClockInAsync_RejectsMismatchedMembershipWithoutRequestingTime()
    {
        await using var setup = await CreateEmployeeAsync();
        var otherActor = setup.Actor with { MembershipId = Guid.NewGuid() };
        var clock = new FakeIsraelClock(Reading("2026-11-19T08:05:42Z", "timeapi-io"));
        var service = CreateService(setup.DbContext, clock);

        await AssertThrowsAsync<AttendanceAccessDeniedException>(() =>
            service.ClockInAsync(otherActor, CancellationToken.None));

        Assert.AreEqual(0, clock.Calls);
        Assert.AreEqual(0, await setup.DbContext.WorkShifts.CountAsync());
    }

    [TestMethod]
    public async Task ClockInAsync_FailsClosedWhenNoExternalTimeReadingIsAvailable()
    {
        await using var setup = await CreateEmployeeAsync();
        var clock = new FakeIsraelClock(failWithClockUnavailable: true);
        var service = CreateService(setup.DbContext, clock);

        await AssertThrowsAsync<AttendanceUnavailableException>(() =>
            service.ClockInAsync(setup.Actor, CancellationToken.None));

        Assert.AreEqual(1, clock.Calls);
        Assert.AreEqual(0, await setup.DbContext.WorkShifts.CountAsync());
    }

    [TestMethod]
    public async Task PersonalSchedule_ExcludedDefaultDayRejectsLivePunchAndCorrection()
    {
        await using var setup = await CreateEmployeeAsync();
        setup.DbContext.EmployeeMonthlyWorkSchedules.Add(EmployeeMonthlyWorkSchedule.Create(
            setup.Actor.CompanyId, setup.Actor.EmployeeProfileId, 2026, 11,
            setup.Actor.MembershipId, [], CreatedAtUtc));
        await setup.DbContext.SaveChangesAsync();
        var live = CreateService(setup.DbContext, new FakeIsraelClock(Reading("2026-11-19T18:00:00Z", "test")));
        await AssertThrowsAsync<AttendanceConflictException>(() => live.ClockInAsync(setup.Actor, CancellationToken.None));
        var corrections = new AttendanceCorrectionService(setup.DbContext,
            new FakeIsraelClock(Reading("2026-11-19T18:00:00Z", "test")),
            new Microsoft.Extensions.Time.Testing.FakeTimeProvider(CreatedAtUtc), new TestCompanyTimeZoneRegistry());
        try
        {
            await corrections.SubmitAsync(new SubmitAttendanceCorrectionCommand(setup.Actor,
                new DateOnly(2026, 11, 19), new TimeOnly(9, 0), null, false), CancellationToken.None);
            Assert.Fail("Excluded personal workday must reject correction.");
        }
        catch (AttendanceCorrectionConflictException exception)
        {
            StringAssert.Contains(exception.Message, "not configured this date as a workday");
        }
        Assert.AreEqual(0, await setup.DbContext.WorkShifts.CountAsync());
        Assert.AreEqual(0, await setup.DbContext.AttendanceCorrectionRequests.CountAsync());
    }

    [TestMethod]
    public async Task PersonalSchedule_OverridesDefaultAndCalendarShowsExcludedDay()
    {
        await using var setup = await CreateEmployeeAsync();
        setup.DbContext.EmployeeMonthlyWorkSchedules.Add(EmployeeMonthlyWorkSchedule.Create(
            setup.Actor.CompanyId, setup.Actor.EmployeeProfileId, 2026, 11,
            setup.Actor.MembershipId, [new DateOnly(2026, 11, 20)], CreatedAtUtc));
        await setup.DbContext.SaveChangesAsync();
        var service = CreateService(setup.DbContext, new FakeIsraelClock(
            Reading("2026-11-20T08:00:00Z", "test"), Reading("2026-11-20T08:00:00Z", "test")));
        var calendar = await service.GetCalendarAsync(setup.Actor, 2026, 11, CancellationToken.None);
        Assert.AreEqual(false, calendar.Days.Single(day => day.Date == new DateOnly(2026, 11, 19)).IsWorkDay);
        Assert.AreEqual(true, calendar.Days.Single(day => day.Date == new DateOnly(2026, 11, 20)).IsWorkDay);
        var result = await service.ClockInAsync(setup.Actor, CancellationToken.None);
        Assert.AreEqual("ClockedIn", result.Status);
    }

    private static async Task<TestEmployee> CreateEmployeeAsync()
    {
        var options = new DbContextOptionsBuilder<PersonalAttendanceDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var dbContext = new PersonalAttendanceDbContext(options);
        var userId = Guid.NewGuid();
        var company = Company.Create("TEST-01", "Test Company", CreatedAtUtc);
        var membership = CompanyMembership.Create(company.Id, userId, null, CreatedAtUtc);
        membership.AssignRole(CompanyRole.Employee);
        var profile = EmployeeProfile.Create(
            company.Id,
            membership.MembershipId,
            "Test Employee",
            "EMP-001",
            CreatedAtUtc);
        var payrollSettings = CompanyPayrollSettings.Create(
            company.Id,
            1,
            "Asia/Jerusalem",
            new TimeOnly(9, 0),
            new TimeOnly(17, 0),
            [DayOfWeek.Thursday],
            membership.MembershipId,
            CreatedAtUtc);

        dbContext.AddRange(company, membership, profile, payrollSettings);
        await dbContext.SaveChangesAsync();

        return new TestEmployee(
            dbContext,
            new AttendanceActor(userId, company.Id, membership.MembershipId, profile.EmployeeProfileId));
    }

    private static WorkShift CreateOpenShift(AttendanceActor actor) => WorkShift.ClockIn(
        actor.CompanyId,
        actor.EmployeeProfileId,
        Instant("2026-11-19T08:05:00Z"),
        "timeapi-io",
        Instant("2026-11-19T08:04:59Z"));

    private static ClockReading Reading(string utcTime, string provider)
    {
        var utc = Instant(utcTime);
        return new ClockReading(
            "Asia/Jerusalem",
            provider,
            utc,
            TimeZoneInfo.ConvertTime(utc, TimeZoneInfo.FindSystemTimeZoneById("Asia/Jerusalem")),
            utc.AddSeconds(-2),
            2_000,
            100,
            1_000);
    }

    private static AttendanceService CreateService(
        PersonalAttendanceDbContext dbContext,
        ICurrentAttendanceClock clock) =>
        new(dbContext, clock, new TestCompanyTimeZoneRegistry());

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

    private sealed class TestEmployee(
        PersonalAttendanceDbContext dbContext,
        AttendanceActor actor) : IAsyncDisposable
    {
        public PersonalAttendanceDbContext DbContext { get; } = dbContext;
        public AttendanceActor Actor { get; } = actor;

        public ValueTask DisposeAsync() => DbContext.DisposeAsync();
    }

    private sealed class FakeIsraelClock(
        params ClockReading[] readings) : ICurrentAttendanceClock
    {
        private readonly Queue<ClockReading> _readings = new(readings);

        public FakeIsraelClock(bool failWithClockUnavailable)
            : this()
        {
            FailWithClockUnavailable = failWithClockUnavailable;
        }

        public int Calls { get; private set; }
        private bool FailWithClockUnavailable { get; }

        public Task<ClockReading> GetCurrentTimeAsync(string timeZoneId, CancellationToken cancellationToken)
        {
            Calls++;
            if (FailWithClockUnavailable)
            {
                throw new ClockUnavailableException();
            }

            return Task.FromResult(_readings.Dequeue());
        }

        public Task<ClockReading> GetCurrentTimeForAttendanceAsync(string timeZoneId, CancellationToken cancellationToken) =>
            GetCurrentTimeAsync(timeZoneId, cancellationToken);
    }

    private sealed class TestCompanyTimeZoneRegistry : ICompanyTimeZoneRegistry
    {
        public string DefaultTimeZoneId => "Asia/Jerusalem";

        public string Register(string timeZoneId) => TimeZoneInfo.FindSystemTimeZoneById(timeZoneId).Id;

        public IReadOnlyList<string> GetRegisteredTimeZones() => [DefaultTimeZoneId];
    }
}
