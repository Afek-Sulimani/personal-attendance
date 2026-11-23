using Personal.Attendance.Domain.Enums;

namespace Personal.Attendance.Domain.Entities;

public sealed class EmployeeProfile
{
    private EmployeeProfile() { }

    public Guid EmployeeProfileId { get; private set; }
    public Guid CompanyId { get; private set; }
    public Guid MembershipId { get; private set; }
    public string DisplayName { get; private set; } = string.Empty;
    public string? EmployeeNumber { get; private set; }
    public EmployeeCompensationType? CompensationType { get; private set; }
    public decimal? HourlyRate { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset UpdatedAtUtc { get; private set; }

    public CompanyMembership Membership { get; private set; } = null!;
    public ICollection<WorkShift> WorkShifts { get; } = new List<WorkShift>();
    public ICollection<EmployeeMonthlyWorkSchedule> MonthlyWorkSchedules { get; } = new List<EmployeeMonthlyWorkSchedule>();

    public static EmployeeProfile Create(
        Guid companyId,
        Guid membershipId,
        string displayName,
        string? employeeNumber,
        DateTimeOffset createdAtUtc)
    {
        if (companyId == Guid.Empty) throw new ArgumentException("Company ID is required.", nameof(companyId));
        if (membershipId == Guid.Empty) throw new ArgumentException("Membership ID is required.", nameof(membershipId));
        ArgumentException.ThrowIfNullOrWhiteSpace(displayName);
        if (createdAtUtc.Offset != TimeSpan.Zero) throw new ArgumentException("The value must use UTC offset zero.", nameof(createdAtUtc));

        return new EmployeeProfile
        {
            EmployeeProfileId = Guid.NewGuid(),
            CompanyId = companyId,
            MembershipId = membershipId,
            DisplayName = displayName.Trim(),
            EmployeeNumber = string.IsNullOrWhiteSpace(employeeNumber) ? null : employeeNumber.Trim(),
            CreatedAtUtc = createdAtUtc,
            UpdatedAtUtc = createdAtUtc
        };
    }

    public void UpdateCompensation(
        EmployeeCompensationType compensationType,
        decimal? hourlyRate,
        DateTimeOffset updatedAtUtc)
    {
        if (!Enum.IsDefined(compensationType))
        {
            throw new ArgumentOutOfRangeException(nameof(compensationType));
        }
        if (updatedAtUtc.Offset != TimeSpan.Zero)
        {
            throw new ArgumentException("The value must use UTC offset zero.", nameof(updatedAtUtc));
        }

        if (compensationType == EmployeeCompensationType.Hourly)
        {
            if (hourlyRate is null or <= 0 ||
                hourlyRate > 9_999_999_999_999_999.99m ||
                decimal.Round(hourlyRate.Value, 2, MidpointRounding.ToZero) != hourlyRate.Value)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(hourlyRate),
                    "An hourly rate must be a positive amount with no more than two decimal places.");
            }
        }
        else if (hourlyRate is not null)
        {
            throw new ArgumentException("A global salary does not have an hourly rate.", nameof(hourlyRate));
        }

        CompensationType = compensationType;
        HourlyRate = hourlyRate;
        UpdatedAtUtc = updatedAtUtc;
    }
}
