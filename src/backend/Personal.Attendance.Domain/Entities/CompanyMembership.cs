using Personal.Attendance.Domain.Enums;

namespace Personal.Attendance.Domain.Entities;

public sealed class CompanyMembership
{
    private CompanyMembership() { }

    public Guid MembershipId { get; private set; }
    public Guid CompanyId { get; private set; }
    public Guid UserId { get; private set; }
    public Guid? ManagerMembershipId { get; private set; }
    public bool IsActive { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset? DeactivatedAtUtc { get; private set; }

    public Company Company { get; private set; } = null!;
    public ICollection<MembershipRole> Roles { get; } = new List<MembershipRole>();
    public EmployeeProfile? EmployeeProfile { get; private set; }

    public static CompanyMembership Create(
        Guid companyId,
        Guid userId,
        Guid? managerMembershipId,
        DateTimeOffset createdAtUtc)
    {
        if (companyId == Guid.Empty) throw new ArgumentException("Company ID is required.", nameof(companyId));
        if (userId == Guid.Empty) throw new ArgumentException("User ID is required.", nameof(userId));
        if (managerMembershipId == Guid.Empty) throw new ArgumentException("Manager ID cannot be empty.", nameof(managerMembershipId));
        if (createdAtUtc.Offset != TimeSpan.Zero) throw new ArgumentException("The value must use UTC offset zero.", nameof(createdAtUtc));

        return new CompanyMembership
        {
            MembershipId = Guid.NewGuid(),
            CompanyId = companyId,
            UserId = userId,
            ManagerMembershipId = managerMembershipId,
            IsActive = true,
            CreatedAtUtc = createdAtUtc
        };
    }

    public void AssignRole(CompanyRole role)
    {
        if (Roles.Any(existing => existing.Role == role)) return;
        Roles.Add(new MembershipRole(MembershipId, role));
    }
}
