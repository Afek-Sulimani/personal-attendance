using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Personal.Attendance.Domain.Entities;

namespace Personal.Attendance.Infrastructure.Persistence.Configurations;

internal sealed class EmployeeMonthlyWorkScheduleConfiguration : IEntityTypeConfiguration<EmployeeMonthlyWorkSchedule>
{
    public void Configure(EntityTypeBuilder<EmployeeMonthlyWorkSchedule> builder)
    {
        builder.ToTable("ATTENDANCE_EMPLOYEE_MONTHLY_WORK_SCHEDULES", table =>
        {
            table.HasCheckConstraint(
                "CK_ATTENDANCE_EMPLOYEE_MONTHLY_WORK_SCHEDULES_MONTH",
                "[Month] >= 1 AND [Month] <= 12");
            table.HasCheckConstraint(
                "CK_ATTENDANCE_EMPLOYEE_MONTHLY_WORK_SCHEDULES_YEAR",
                "[Year] >= 2000 AND [Year] <= 9999");
        });
        builder.HasKey(schedule => schedule.ScheduleId);
        builder.HasAlternateKey(schedule => new { schedule.CompanyId, schedule.ScheduleId });
        builder.Property(schedule => schedule.CreatedAtUtc).HasColumnType("datetimeoffset(7)").IsRequired();
        builder.Property(schedule => schedule.UpdatedAtUtc).HasColumnType("datetimeoffset(7)").IsRequired();
        builder.HasIndex(schedule => new
            {
                schedule.CompanyId,
                schedule.EmployeeProfileId,
                schedule.Year,
                schedule.Month
            })
            .IsUnique();
        builder.HasOne(schedule => schedule.EmployeeProfile)
            .WithMany(profile => profile.MonthlyWorkSchedules)
            .HasForeignKey(schedule => new { schedule.CompanyId, schedule.EmployeeProfileId })
            .HasPrincipalKey(profile => new { profile.CompanyId, profile.EmployeeProfileId })
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<CompanyMembership>()
            .WithMany()
            .HasForeignKey(schedule => new { schedule.CompanyId, schedule.ConfiguredByMembershipId })
            .HasPrincipalKey(membership => new { membership.CompanyId, membership.MembershipId })
            .OnDelete(DeleteBehavior.Restrict);
    }
}
