using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Personal.Attendance.Domain.Entities;

namespace Personal.Attendance.Infrastructure.Persistence.Configurations;

internal sealed class CompanyDefaultWorkDayConfiguration : IEntityTypeConfiguration<CompanyDefaultWorkDay>
{
    public void Configure(EntityTypeBuilder<CompanyDefaultWorkDay> builder)
    {
        builder.ToTable("ATTENDANCE_COMPANY_DEFAULT_WORK_DAYS");
        builder.HasKey(day => new { day.CompanyId, day.DayOfWeek });
        builder.Property(day => day.DayOfWeek).HasConversion<int>();
        builder.HasOne(day => day.Settings)
            .WithMany(settings => settings.DefaultWorkDays)
            .HasForeignKey(day => day.CompanyId)
            .HasPrincipalKey(settings => settings.CompanyId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
