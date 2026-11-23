using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Personal.Attendance.Domain.Entities;

namespace Personal.Attendance.Infrastructure.Persistence.Configurations;

internal sealed class EmployeeMonthlyWorkDayConfiguration : IEntityTypeConfiguration<EmployeeMonthlyWorkDay>
{
    public void Configure(EntityTypeBuilder<EmployeeMonthlyWorkDay> builder)
    {
        builder.ToTable("ATTENDANCE_EMPLOYEE_MONTHLY_WORK_DAYS");
        builder.HasKey(day => day.WorkDayId);
        builder.Property(day => day.WorkDate).HasColumnType("date").IsRequired();
        builder.HasOne(day => day.Schedule)
            .WithMany(schedule => schedule.WorkingDays)
            .HasForeignKey(day => new { day.CompanyId, day.ScheduleId })
            .HasPrincipalKey(schedule => new { schedule.CompanyId, schedule.ScheduleId })
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(day => new { day.CompanyId, day.ScheduleId, day.WorkDate }).IsUnique();
    }
}
