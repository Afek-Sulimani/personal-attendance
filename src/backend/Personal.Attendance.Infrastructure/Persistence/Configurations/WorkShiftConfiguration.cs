using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Personal.Attendance.Domain.Entities;

namespace Personal.Attendance.Infrastructure.Persistence.Configurations;

internal sealed class WorkShiftConfiguration : IEntityTypeConfiguration<WorkShift>
{
    public void Configure(EntityTypeBuilder<WorkShift> builder)
    {
        // Keep core time invariants in SQL as a final guard against bugs and concurrent application requests.
        builder.ToTable("ATTENDANCE_WORK_SHIFTS", table =>
        {
            // A shift cannot end before it starts.
            table.HasCheckConstraint(
                "CK_ATTENDANCE_WORK_SHIFTS_CLOCK_ORDER",
                "[ClockOutAtUtc] IS NULL OR [ClockOutAtUtc] >= [ClockInAtUtc]");
            // Match the domain's fixed maximum even if a write bypasses AttendanceService.
            table.HasCheckConstraint(
                "CK_ATTENDANCE_WORK_SHIFTS_MAX_DURATION",
                $"[ClockOutAtUtc] IS NULL OR [ClockOutAtUtc] <= DATEADD(hour, {WorkShift.MaximumDurationHours}, [ClockInAtUtc])");
            // Live Clock In rows require external provenance; manual corrections must not claim a provider sample.
            table.HasCheckConstraint(
                "CK_ATTENDANCE_WORK_SHIFTS_CLOCK_IN_SOURCE",
                "([ClockInSource] = 'ExternalApi' AND [ClockInProvider] IS NOT NULL AND [ClockInProviderSampleUtc] IS NOT NULL) OR " +
                "([ClockInSource] IN ('ManagerApprovedCorrection', 'AdminDirectCorrection') AND [ClockInProvider] IS NULL AND [ClockInProviderSampleUtc] IS NULL)");
            // Keep Clock Out timestamp, zone, source, and provider metadata mutually consistent.
            table.HasCheckConstraint(
                "CK_ATTENDANCE_WORK_SHIFTS_CLOCK_OUT_SOURCE",
                "([ClockOutAtUtc] IS NULL AND [ClockOutTimeZoneId] IS NULL AND [ClockOutSource] IS NULL AND [ClockOutProvider] IS NULL AND [ClockOutProviderSampleUtc] IS NULL) OR " +
                "([ClockOutAtUtc] IS NOT NULL AND [ClockOutTimeZoneId] IS NOT NULL AND [ClockOutSource] = 'ExternalApi' AND [ClockOutProvider] IS NOT NULL AND [ClockOutProviderSampleUtc] IS NOT NULL) OR " +
                "([ClockOutAtUtc] IS NOT NULL AND [ClockOutTimeZoneId] IS NOT NULL AND [ClockOutSource] IN ('ManagerApprovedCorrection', 'AdminDirectCorrection') AND [ClockOutProvider] IS NULL AND [ClockOutProviderSampleUtc] IS NULL)");
        });
        builder.HasKey(shift => shift.WorkShiftId);
        builder.HasAlternateKey(shift => new { shift.CompanyId, shift.WorkShiftId });
        builder.HasAlternateKey(shift => new { shift.CompanyId, shift.WorkShiftId, shift.EmployeeProfileId });
        builder.Property(shift => shift.ClockInAtUtc).HasColumnType("datetimeoffset(7)").IsRequired();
        builder.Property(shift => shift.WorkDate).HasColumnType("date").IsRequired();
        builder.Property(shift => shift.ClockInTimeZoneId).HasMaxLength(100).IsRequired();
        builder.Property(shift => shift.ClockInSource).HasConversion<string>().HasMaxLength(40).IsRequired();
        builder.Property(shift => shift.ClockInProvider).HasMaxLength(100);
        builder.Property(shift => shift.ClockInProviderSampleUtc).HasColumnType("datetimeoffset(7)");
        builder.Property(shift => shift.ClockOutAtUtc).HasColumnType("datetimeoffset(7)");
        builder.Property(shift => shift.ClockOutTimeZoneId).HasMaxLength(100);
        builder.Property(shift => shift.ClockOutSource).HasConversion<string>().HasMaxLength(40);
        builder.Property(shift => shift.ClockOutProvider).HasMaxLength(100);
        builder.Property(shift => shift.ClockOutProviderSampleUtc).HasColumnType("datetimeoffset(7)");
        // Let SQL Server detect edits made after another request loaded the same shift.
        builder.Property(shift => shift.RowVersion).IsRowVersion();

        // Support company/employee time-ordered lookups used by attendance views and reports.
        builder.HasIndex(shift => new { shift.CompanyId, shift.EmployeeProfileId, shift.ClockInAtUtc });

        // Enforce exactly one shift per company-local work date, whether the existing shift is open or closed.
        builder.HasIndex(shift => new { shift.CompanyId, shift.EmployeeProfileId, shift.WorkDate }).IsUnique();

        // Enforce at most one open shift per employee at the database layer, even during concurrent Clock In calls.
        builder.HasIndex(shift => new { shift.CompanyId, shift.EmployeeProfileId })
            .IsUnique()
            .HasFilter("[ClockOutAtUtc] IS NULL");
        builder.HasOne(shift => shift.EmployeeProfile)
            .WithMany(profile => profile.WorkShifts)
            .HasForeignKey(shift => new { shift.CompanyId, shift.EmployeeProfileId })
            .HasPrincipalKey(profile => new { profile.CompanyId, profile.EmployeeProfileId })
            .OnDelete(DeleteBehavior.Restrict);
    }
}
