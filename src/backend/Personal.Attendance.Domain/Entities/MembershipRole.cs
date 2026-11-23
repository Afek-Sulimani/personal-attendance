using Personal.Attendance.Domain.Enums;

namespace Personal.Attendance.Domain.Entities;

public sealed class MembershipRole
{
    private MembershipRole() { }

    internal MembershipRole(Guid membershipId, CompanyRole role)
    {
        MembershipId = membershipId;
        Role = role;
    }

    public Guid MembershipId { get; private set; }
    public CompanyRole Role { get; private set; }
    public CompanyMembership Membership { get; private set; } = null!;
}
