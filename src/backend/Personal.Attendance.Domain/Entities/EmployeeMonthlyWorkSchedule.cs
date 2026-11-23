namespace Personal.Attendance.Domain.Entities;

public sealed class EmployeeMonthlyWorkSchedule
{
    private EmployeeMonthlyWorkSchedule() { }

    public Guid ScheduleId { get; private set; }
    public Guid CompanyId { get; private set; }
    public Guid EmployeeProfileId { get; private set; }
    public int Year { get; private set; }
    public int Month { get; private set; }
    public Guid ConfiguredByMembershipId { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset UpdatedAtUtc { get; private set; }

    public EmployeeProfile EmployeeProfile { get; private set; } = null!;
    public ICollection<EmployeeMonthlyWorkDay> WorkingDays { get; } = new List<EmployeeMonthlyWorkDay>();

    public static EmployeeMonthlyWorkSchedule Create(
        Guid companyId,
        Guid employeeProfileId,
        int year,
        int month,
        Guid configuredByMembershipId,
        IEnumerable<DateOnly> workingDates,
        DateTimeOffset configuredAtUtc)
    {
        ValidatePeriod(year, month);
        ValidateUtc(configuredAtUtc, nameof(configuredAtUtc));
        ArgumentNullException.ThrowIfNull(workingDates);

        var schedule = new EmployeeMonthlyWorkSchedule
        {
            ScheduleId = Guid.NewGuid(),
            CompanyId = companyId,
            EmployeeProfileId = employeeProfileId,
            Year = year,
            Month = month,
            ConfiguredByMembershipId = configuredByMembershipId,
            CreatedAtUtc = configuredAtUtc,
            UpdatedAtUtc = configuredAtUtc
        };
        schedule.ReplaceWorkingDates(workingDates, configuredAtUtc);
        return schedule;
    }

    public void ReplaceWorkingDates(IEnumerable<DateOnly> workingDates, DateTimeOffset updatedAtUtc)
    {
        ArgumentNullException.ThrowIfNull(workingDates);
        ValidateUtc(updatedAtUtc, nameof(updatedAtUtc));

        var uniqueDates = workingDates.Distinct().Order().ToArray();
        if (uniqueDates.Any(date => date.Year != Year || date.Month != Month))
        {
            throw new ArgumentException("Every work date must belong to the configured month.", nameof(workingDates));
        }

        WorkingDays.Clear();
        foreach (var workDate in uniqueDates)
        {
            WorkingDays.Add(EmployeeMonthlyWorkDay.Create(CompanyId, ScheduleId, workDate));
        }

        UpdatedAtUtc = updatedAtUtc;
    }

    private static void ValidatePeriod(int year, int month)
    {
        if (year is < 2000 or > 9999) throw new ArgumentOutOfRangeException(nameof(year));
        if (month is < 1 or > 12) throw new ArgumentOutOfRangeException(nameof(month));
    }

    private static void ValidateUtc(DateTimeOffset value, string parameterName)
    {
        if (value.Offset != TimeSpan.Zero)
        {
            throw new ArgumentException("The value must use UTC offset zero.", parameterName);
        }
    }
}
