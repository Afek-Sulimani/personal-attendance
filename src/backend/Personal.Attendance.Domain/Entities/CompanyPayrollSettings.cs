namespace Personal.Attendance.Domain.Entities;

public sealed class CompanyPayrollSettings
{
    private CompanyPayrollSettings() { }

    public Guid CompanyId { get; private set; }
    public int PayrollPeriodStartDay { get; private set; }
    public string TimeZoneId { get; private set; } = string.Empty;
    public TimeOnly DefaultWorkStartTime { get; private set; }
    public TimeOnly DefaultWorkEndTime { get; private set; }
    public Guid ConfiguredByMembershipId { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset UpdatedAtUtc { get; private set; }

    public Company Company { get; private set; } = null!;
    public ICollection<CompanyDefaultWorkDay> DefaultWorkDays { get; } = new List<CompanyDefaultWorkDay>();

    public static CompanyPayrollSettings Create(
        Guid companyId,
        int payrollPeriodStartDay,
        string timeZoneId,
        TimeOnly defaultWorkStartTime,
        TimeOnly defaultWorkEndTime,
        IEnumerable<DayOfWeek> defaultWorkDays,
        Guid configuredByMembershipId,
        DateTimeOffset configuredAtUtc)
    {
        if (companyId == Guid.Empty) throw new ArgumentException("Company ID is required.", nameof(companyId));
        if (configuredByMembershipId == Guid.Empty) throw new ArgumentException("A company membership is required.", nameof(configuredByMembershipId));
        if (payrollPeriodStartDay is < 1 or > 31) throw new ArgumentOutOfRangeException(nameof(payrollPeriodStartDay));
        ValidateTimeZoneId(timeZoneId);
        ValidateUtc(configuredAtUtc, nameof(configuredAtUtc));

        var uniqueDays = ValidateWorkDays(defaultWorkDays);
        var settings = new CompanyPayrollSettings
        {
            CompanyId = companyId,
            PayrollPeriodStartDay = payrollPeriodStartDay,
            TimeZoneId = timeZoneId.Trim(),
            DefaultWorkStartTime = defaultWorkStartTime,
            DefaultWorkEndTime = defaultWorkEndTime,
            ConfiguredByMembershipId = configuredByMembershipId,
            CreatedAtUtc = configuredAtUtc,
            UpdatedAtUtc = configuredAtUtc
        };
        settings.ReplaceDefaultWorkDays(uniqueDays);
        return settings;
    }

    public void Update(
        int payrollPeriodStartDay,
        string timeZoneId,
        TimeOnly defaultWorkStartTime,
        TimeOnly defaultWorkEndTime,
        IEnumerable<DayOfWeek> defaultWorkDays,
        Guid configuredByMembershipId,
        DateTimeOffset updatedAtUtc)
    {
        if (payrollPeriodStartDay is < 1 or > 31) throw new ArgumentOutOfRangeException(nameof(payrollPeriodStartDay));
        ValidateTimeZoneId(timeZoneId);
        if (configuredByMembershipId == Guid.Empty) throw new ArgumentException("A company membership is required.", nameof(configuredByMembershipId));
        ValidateUtc(updatedAtUtc, nameof(updatedAtUtc));

        PayrollPeriodStartDay = payrollPeriodStartDay;
        TimeZoneId = timeZoneId.Trim();
        DefaultWorkStartTime = defaultWorkStartTime;
        DefaultWorkEndTime = defaultWorkEndTime;
        ConfiguredByMembershipId = configuredByMembershipId;
        UpdatedAtUtc = updatedAtUtc;
        ReplaceDefaultWorkDays(ValidateWorkDays(defaultWorkDays));
    }

    private void ReplaceDefaultWorkDays(IEnumerable<DayOfWeek> workDays)
    {
        DefaultWorkDays.Clear();
        foreach (var workDay in workDays)
        {
            DefaultWorkDays.Add(CompanyDefaultWorkDay.Create(CompanyId, workDay));
        }
    }

    private static DayOfWeek[] ValidateWorkDays(IEnumerable<DayOfWeek> workDays)
    {
        ArgumentNullException.ThrowIfNull(workDays);
        var uniqueDays = workDays.Distinct().Order().ToArray();
        if (uniqueDays.Length == 0 || uniqueDays.Any(day => !Enum.IsDefined(day)))
        {
            throw new ArgumentException("Select at least one valid workday.", nameof(workDays));
        }

        return uniqueDays;
    }

    private static void ValidateUtc(DateTimeOffset value, string parameterName)
    {
        if (value.Offset != TimeSpan.Zero) throw new ArgumentException("The value must use UTC offset zero.", parameterName);
    }

    private static void ValidateTimeZoneId(string timeZoneId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(timeZoneId);
        if (timeZoneId.Trim().Length > 100)
        {
            throw new ArgumentOutOfRangeException(nameof(timeZoneId), "Time zone IDs cannot exceed 100 characters.");
        }
    }
}
