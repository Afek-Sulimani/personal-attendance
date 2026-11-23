using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Personal.Attendance.Domain.Entities;

namespace Personal.Attendance.Infrastructure.Persistence.Configurations;

internal sealed class EmployeeProfileConfiguration : IEntityTypeConfiguration<EmployeeProfile>
{
    public void Configure(EntityTypeBuilder<EmployeeProfile> builder)
    {
        builder.ToTable("ATTENDANCE_EMPLOYEE_PROFILES", table => table.HasCheckConstraint(
            "CK_ATTENDANCE_EMPLOYEE_PROFILES_COMPENSATION",
            "([CompensationType] IS NULL AND [HourlyRate] IS NULL) OR " +
            "([CompensationType] = N'Global' AND [HourlyRate] IS NULL) OR " +
            "([CompensationType] = N'Hourly' AND [HourlyRate] IS NOT NULL AND [HourlyRate] > 0)"));
        builder.HasKey(profile => profile.EmployeeProfileId);
        builder.HasAlternateKey(profile => new { profile.CompanyId, profile.EmployeeProfileId });
        builder.Property(profile => profile.DisplayName).HasMaxLength(200).IsRequired();
        builder.Property(profile => profile.EmployeeNumber).HasMaxLength(100);
        builder.Property(profile => profile.CompensationType).HasConversion<string>().HasMaxLength(16);
        builder.Property(profile => profile.HourlyRate).HasColumnType("decimal(18,2)");
        builder.Property(profile => profile.CreatedAtUtc).HasColumnType("datetimeoffset(7)").IsRequired();
        builder.Property(profile => profile.UpdatedAtUtc).HasColumnType("datetimeoffset(7)").IsRequired();
        builder.HasIndex(profile => profile.MembershipId).IsUnique();
        builder.HasIndex(profile => new { profile.CompanyId, profile.EmployeeNumber })
            .IsUnique()
            .HasFilter("[EmployeeNumber] IS NOT NULL");
        builder.HasOne(profile => profile.Membership)
            .WithOne(membership => membership.EmployeeProfile)
            .HasForeignKey<EmployeeProfile>(profile => new { profile.CompanyId, profile.MembershipId })
            .HasPrincipalKey<CompanyMembership>(membership => new { membership.CompanyId, membership.MembershipId })
            .OnDelete(DeleteBehavior.Restrict);
    }
}
