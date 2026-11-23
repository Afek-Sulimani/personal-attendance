using Personal.Attendance.Domain.Enums;

namespace Personal.Attendance.Domain.Entities;

public sealed class EmployeeInvitation
{
    private EmployeeInvitation() { }

    public Guid InvitationId { get; private set; }
    public Guid CompanyId { get; private set; }
    public Guid? CreatedByMembershipId { get; private set; }
    public Guid? ManagerMembershipId { get; private set; }
    public string InvitedEmail { get; private set; } = string.Empty;
    public string NormalizedEmail { get; private set; } = string.Empty;
    public string DisplayName { get; private set; } = string.Empty;
    public string? EmployeeNumber { get; private set; }
    public string AssignedRoles { get; private set; } = string.Empty;
    public string TokenHash { get; private set; } = string.Empty;
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset ExpiresAtUtc { get; private set; }
    public DateTimeOffset? AcceptedAtUtc { get; private set; }
    public Guid? AcceptedUserId { get; private set; }
    public DateTimeOffset? RevokedAtUtc { get; private set; }
    public byte[] RowVersion { get; private set; } = [];

    public static EmployeeInvitation Create(
        Guid companyId,
        Guid? createdByMembershipId,
        Guid? managerMembershipId,
        string email,
        string displayName,
        string? employeeNumber,
        IEnumerable<CompanyRole> roles,
        string tokenHash,
        DateTimeOffset createdAtUtc,
        DateTimeOffset expiresAtUtc)
    {
        if (companyId == Guid.Empty) throw new ArgumentException("Company ID is required.", nameof(companyId));
        if (createdByMembershipId == Guid.Empty) throw new ArgumentException("Creator membership ID cannot be empty.", nameof(createdByMembershipId));
        if (managerMembershipId == Guid.Empty) throw new ArgumentException("Manager membership ID cannot be empty.", nameof(managerMembershipId));
        ArgumentException.ThrowIfNullOrWhiteSpace(email);
        ArgumentException.ThrowIfNullOrWhiteSpace(displayName);
        ArgumentException.ThrowIfNullOrWhiteSpace(tokenHash);
        EnsureUtc(createdAtUtc, nameof(createdAtUtc));
        EnsureUtc(expiresAtUtc, nameof(expiresAtUtc));

        if (expiresAtUtc <= createdAtUtc)
        {
            throw new ArgumentException("Invitation expiration must be after creation.", nameof(expiresAtUtc));
        }

        var assignedRoles = roles?.Distinct().Order().ToArray()
            ?? throw new ArgumentNullException(nameof(roles));
        if (assignedRoles.Length == 0)
        {
            throw new ArgumentException("At least one role is required.", nameof(roles));
        }

        var normalizedEmail = email.Trim().ToUpperInvariant();
        return new EmployeeInvitation
        {
            InvitationId = Guid.NewGuid(),
            CompanyId = companyId,
            CreatedByMembershipId = createdByMembershipId,
            ManagerMembershipId = managerMembershipId,
            InvitedEmail = email.Trim(),
            NormalizedEmail = normalizedEmail,
            DisplayName = displayName.Trim(),
            EmployeeNumber = string.IsNullOrWhiteSpace(employeeNumber) ? null : employeeNumber.Trim(),
            AssignedRoles = string.Join(',', assignedRoles.Select(role => role.ToString())),
            TokenHash = tokenHash,
            CreatedAtUtc = createdAtUtc,
            ExpiresAtUtc = expiresAtUtc
        };
    }

    public IReadOnlyList<CompanyRole> GetAssignedRoles() => AssignedRoles
        .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        .Select(value => Enum.Parse<CompanyRole>(value, ignoreCase: true))
        .ToArray();

    public bool IsUsableAt(DateTimeOffset nowUtc)
    {
        EnsureUtc(nowUtc, nameof(nowUtc));
        return AcceptedAtUtc is null && RevokedAtUtc is null && ExpiresAtUtc > nowUtc;
    }

    public void Revoke(DateTimeOffset revokedAtUtc)
    {
        EnsureUtc(revokedAtUtc, nameof(revokedAtUtc));
        if (AcceptedAtUtc is not null) return;
        RevokedAtUtc = revokedAtUtc;
    }

    public void MarkAccepted(Guid userId, DateTimeOffset acceptedAtUtc)
    {
        if (userId == Guid.Empty) throw new ArgumentException("User ID is required.", nameof(userId));
        EnsureUtc(acceptedAtUtc, nameof(acceptedAtUtc));
        if (!IsUsableAt(acceptedAtUtc))
        {
            throw new InvalidOperationException("The invitation is no longer usable.");
        }

        AcceptedUserId = userId;
        AcceptedAtUtc = acceptedAtUtc;
    }

    private static void EnsureUtc(DateTimeOffset value, string parameterName)
    {
        if (value.Offset != TimeSpan.Zero)
        {
            throw new ArgumentException("The value must use UTC offset zero.", parameterName);
        }
    }
}
