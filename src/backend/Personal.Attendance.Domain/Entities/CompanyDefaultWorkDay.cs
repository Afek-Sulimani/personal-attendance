namespace Personal.Attendance.Domain.Entities;

public sealed class CompanyDefaultWorkDay
{
    private CompanyDefaultWorkDay() { }

    public Guid CompanyId { get; private set; }
    public DayOfWeek DayOfWeek { get; private set; }

    public CompanyPayrollSettings Settings { get; private set; } = null!;

    public static CompanyDefaultWorkDay Create(Guid companyId, DayOfWeek dayOfWeek)
    {
        if (companyId == Guid.Empty) throw new ArgumentException("Company ID is required.", nameof(companyId));
        if (!Enum.IsDefined(dayOfWeek)) throw new ArgumentOutOfRangeException(nameof(dayOfWeek));

        return new CompanyDefaultWorkDay { CompanyId = companyId, DayOfWeek = dayOfWeek };
    }
}
