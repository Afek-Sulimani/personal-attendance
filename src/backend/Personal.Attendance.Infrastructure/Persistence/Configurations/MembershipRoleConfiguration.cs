using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Personal.Attendance.Domain.Entities;
using Personal.Attendance.Domain.Enums;

namespace Personal.Attendance.Infrastructure.Persistence.Configurations;

internal sealed class MembershipRoleConfiguration : IEntityTypeConfiguration<MembershipRole>
{
    public void Configure(EntityTypeBuilder<MembershipRole> builder)
    {
        builder.ToTable("ATTENDANCE_MEMBERSHIP_ROLES");
        builder.HasKey(role => new { role.MembershipId, role.Role });
        builder.Property(role => role.Role)
            .HasConversion(role => role == CompanyRole.Hr ? "HR" : role.ToString(), value => Enum.Parse<CompanyRole>(value, true))
            .HasMaxLength(40);
        builder.HasOne(role => role.Membership)
            .WithMany(membership => membership.Roles)
            .HasForeignKey(role => role.MembershipId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
