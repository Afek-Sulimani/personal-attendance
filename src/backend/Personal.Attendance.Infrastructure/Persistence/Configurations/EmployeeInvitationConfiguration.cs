using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Personal.Attendance.Domain.Entities;

namespace Personal.Attendance.Infrastructure.Persistence.Configurations;

internal sealed class EmployeeInvitationConfiguration : IEntityTypeConfiguration<EmployeeInvitation>
{
    public void Configure(EntityTypeBuilder<EmployeeInvitation> builder)
    {
        builder.ToTable("ATTENDANCE_EMPLOYEE_INVITATIONS", table =>
            table.HasCheckConstraint(
                "CK_ATTENDANCE_EMPLOYEE_INVITATIONS_Expiration",
                "[ExpiresAtUtc] > [CreatedAtUtc]"));
        builder.HasKey(invitation => invitation.InvitationId);
        builder.Property(invitation => invitation.InvitedEmail).HasMaxLength(256).IsRequired();
        builder.Property(invitation => invitation.NormalizedEmail).HasMaxLength(256).IsRequired();
        builder.Property(invitation => invitation.DisplayName).HasMaxLength(200).IsRequired();
        builder.Property(invitation => invitation.EmployeeNumber).HasMaxLength(100);
        builder.Property(invitation => invitation.AssignedRoles).HasMaxLength(200).IsRequired();
        builder.Property(invitation => invitation.TokenHash).HasColumnType("char(64)").IsRequired();
        builder.Property(invitation => invitation.CreatedAtUtc).HasColumnType("datetimeoffset(7)").IsRequired();
        builder.Property(invitation => invitation.ExpiresAtUtc).HasColumnType("datetimeoffset(7)").IsRequired();
        builder.Property(invitation => invitation.AcceptedAtUtc).HasColumnType("datetimeoffset(7)");
        builder.Property(invitation => invitation.RevokedAtUtc).HasColumnType("datetimeoffset(7)");
        builder.Property(invitation => invitation.RowVersion).IsRowVersion();
        builder.HasIndex(invitation => invitation.TokenHash).IsUnique();
        builder.HasIndex(invitation => new { invitation.CompanyId, invitation.NormalizedEmail });

        builder.HasOne<Company>()
            .WithMany()
            .HasForeignKey(invitation => invitation.CompanyId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<CompanyMembership>()
            .WithMany()
            .HasForeignKey(invitation => new { invitation.CompanyId, invitation.CreatedByMembershipId })
            .HasPrincipalKey(membership => new { membership.CompanyId, membership.MembershipId })
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<CompanyMembership>()
            .WithMany()
            .HasForeignKey(invitation => new { invitation.CompanyId, invitation.ManagerMembershipId })
            .HasPrincipalKey(membership => new { membership.CompanyId, membership.MembershipId })
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<PersonalUser>()
            .WithMany()
            .HasForeignKey(invitation => invitation.AcceptedUserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
