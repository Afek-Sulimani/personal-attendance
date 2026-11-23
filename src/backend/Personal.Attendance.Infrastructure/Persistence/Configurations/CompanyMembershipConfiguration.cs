using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Personal.Attendance.Domain.Entities;

namespace Personal.Attendance.Infrastructure.Persistence.Configurations;

internal sealed class CompanyMembershipConfiguration : IEntityTypeConfiguration<CompanyMembership>
{
    public void Configure(EntityTypeBuilder<CompanyMembership> builder)
    {
        builder.ToTable("ATTENDANCE_COMPANY_MEMBERSHIPS");
        builder.HasKey(membership => membership.MembershipId);
        builder.HasAlternateKey(membership => new { membership.CompanyId, membership.MembershipId });
        builder.Property(membership => membership.CreatedAtUtc).HasColumnType("datetimeoffset(7)").IsRequired();
        builder.Property(membership => membership.DeactivatedAtUtc).HasColumnType("datetimeoffset(7)");
        builder.HasIndex(membership => new { membership.CompanyId, membership.UserId }).IsUnique();
        builder.HasIndex(membership => new { membership.CompanyId, membership.ManagerMembershipId });
        builder.HasOne(membership => membership.Company)
            .WithMany(company => company.Memberships)
            .HasForeignKey(membership => membership.CompanyId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<PersonalUser>()
            .WithMany()
            .HasForeignKey(membership => membership.UserId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<CompanyMembership>()
            .WithMany()
            .HasForeignKey(membership => new { membership.CompanyId, membership.ManagerMembershipId })
            .HasPrincipalKey(manager => new { manager.CompanyId, manager.MembershipId })
            .OnDelete(DeleteBehavior.Restrict);
    }
}
