using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Personal.Attendance.Domain.Entities;

namespace Personal.Attendance.Infrastructure.Persistence.Configurations;

internal sealed class CompanyPayrollSettingsConfiguration : IEntityTypeConfiguration<CompanyPayrollSettings>
{
    public void Configure(EntityTypeBuilder<CompanyPayrollSettings> builder)
    {
        builder.ToTable("ATTENDANCE_COMPANY_PAYROLL_SETTINGS", table =>
            table.HasCheckConstraint(
                "CK_ATTENDANCE_COMPANY_PAYROLL_SETTINGS_PERIOD_START_DAY",
                "[PayrollPeriodStartDay] >= 1 AND [PayrollPeriodStartDay] <= 31"));
        builder.HasKey(settings => settings.CompanyId);
        builder.Property(settings => settings.TimeZoneId).HasMaxLength(100).IsRequired();
        builder.Property(settings => settings.DefaultWorkStartTime).HasColumnType("time(0)").IsRequired();
        builder.Property(settings => settings.DefaultWorkEndTime).HasColumnType("time(0)").IsRequired();
        builder.Property(settings => settings.CreatedAtUtc).HasColumnType("datetimeoffset(7)").IsRequired();
        builder.Property(settings => settings.UpdatedAtUtc).HasColumnType("datetimeoffset(7)").IsRequired();
        builder.HasOne(settings => settings.Company)
            .WithOne(company => company.PayrollSettings)
            .HasForeignKey<CompanyPayrollSettings>(settings => settings.CompanyId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<CompanyMembership>()
            .WithMany()
            .HasForeignKey(settings => new { settings.CompanyId, settings.ConfiguredByMembershipId })
            .HasPrincipalKey(membership => new { membership.CompanyId, membership.MembershipId })
            .OnDelete(DeleteBehavior.Restrict);
    }
}
