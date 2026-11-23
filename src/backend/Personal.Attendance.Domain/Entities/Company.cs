namespace Personal.Attendance.Domain.Entities;

public sealed class Company
{
    private Company() { }

    public Guid Id { get; private set; }
    public string CompanyCode { get; private set; } = string.Empty;
    public string NormalizedCompanyCode { get; private set; } = string.Empty;
    public string Name { get; private set; } = string.Empty;
    public bool IsActive { get; private set; }
    public int NextEmployeeNumber { get; private set; } = 1;
    public DateTimeOffset CreatedAtUtc { get; private set; }

    public ICollection<CompanyMembership> Memberships { get; } = new List<CompanyMembership>();
    public CompanyPayrollSettings? PayrollSettings { get; private set; }

    public static Company Create(string companyCode, string name, DateTimeOffset createdAtUtc)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(companyCode);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        EnsureUtc(createdAtUtc, nameof(createdAtUtc));

        var normalizedCode = companyCode.Trim().ToUpperInvariant();
        if (normalizedCode.Length > 50)
        {
            throw new ArgumentOutOfRangeException(nameof(companyCode), "Company code cannot exceed 50 characters.");
        }

        return new Company
        {
            Id = Guid.NewGuid(),
            CompanyCode = companyCode.Trim(),
            NormalizedCompanyCode = normalizedCode,
            Name = name.Trim(),
            IsActive = true,
            NextEmployeeNumber = 1,
            CreatedAtUtc = createdAtUtc
        };
    }

    public string AllocateEmployeeNumber()
    {
        if (NextEmployeeNumber is < 1 or > 99999)
        {
            throw new InvalidOperationException("The company has no available five-digit employee numbers.");
        }

        return (NextEmployeeNumber++).ToString("D5", System.Globalization.CultureInfo.InvariantCulture);
    }

    private static void EnsureUtc(DateTimeOffset value, string parameterName)
    {
        if (value.Offset != TimeSpan.Zero)
        {
            throw new ArgumentException("The value must use UTC offset zero.", parameterName);
        }
    }
}
