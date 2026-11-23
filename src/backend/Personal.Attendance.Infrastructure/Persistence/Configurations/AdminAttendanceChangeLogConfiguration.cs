using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Personal.Attendance.Domain.Entities;

namespace Personal.Attendance.Infrastructure.Persistence.Configurations;

internal sealed class AdminAttendanceChangeLogConfiguration : IEntityTypeConfiguration<AdminAttendanceChangeLog>
{
    public void Configure(EntityTypeBuilder<AdminAttendanceChangeLog> builder)
    {
        builder.ToTable("ATTENDANCE_ADMIN_ATTENDANCE_CHANGE_LOGS", table =>
        {
            table.HasTrigger("TR_ATTENDANCE_ADMIN_ATTENDANCE_CHANGE_LOGS_PROTECT");
            table.HasCheckConstraint(
                "CK_ATTENDANCE_ADMIN_ATTENDANCE_CHANGE_LOGS_CLOCK_IN_SOURCE",
                "([ClockInSource] = 'ExternalApi' AND [ClockInProvider] IS NOT NULL AND [ClockInProviderSampleUtc] IS NOT NULL) OR " +
                "([ClockInSource] IN ('ManagerApprovedCorrection', 'AdminDirectCorrection') AND [ClockInProvider] IS NULL AND [ClockInProviderSampleUtc] IS NULL)");
            table.HasCheckConstraint(
                "CK_ATTENDANCE_ADMIN_ATTENDANCE_CHANGE_LOGS_CLOCK_OUT_SOURCE",
                "([ClockOutAtUtc] IS NULL AND [ClockOutTimeZoneId] IS NULL AND [ClockOutSource] IS NULL AND [ClockOutProvider] IS NULL AND [ClockOutProviderSampleUtc] IS NULL) OR " +
                "([ClockOutAtUtc] IS NOT NULL AND [ClockOutTimeZoneId] IS NOT NULL AND [ClockOutSource] = 'ExternalApi' AND [ClockOutProvider] IS NOT NULL AND [ClockOutProviderSampleUtc] IS NOT NULL) OR " +
                "([ClockOutAtUtc] IS NOT NULL AND [ClockOutTimeZoneId] IS NOT NULL AND [ClockOutSource] IN ('ManagerApprovedCorrection', 'AdminDirectCorrection') AND [ClockOutProvider] IS NULL AND [ClockOutProviderSampleUtc] IS NULL)");
            table.HasCheckConstraint(
                "CK_ATTENDANCE_ADMIN_ATTENDANCE_CHANGE_LOGS_PREVIOUS_CLOCK_OUT_SOURCE",
                "([PreviousClockOutAtUtc] IS NULL AND [PreviousClockOutTimeZoneId] IS NULL AND [PreviousClockOutSource] IS NULL AND [PreviousClockOutProvider] IS NULL AND [PreviousClockOutProviderSampleUtc] IS NULL) OR " +
                "([PreviousClockOutAtUtc] IS NOT NULL AND [PreviousClockOutTimeZoneId] IS NOT NULL AND [PreviousClockOutSource] = 'ExternalApi' AND [PreviousClockOutProvider] IS NOT NULL AND [PreviousClockOutProviderSampleUtc] IS NOT NULL) OR " +
                "([PreviousClockOutAtUtc] IS NOT NULL AND [PreviousClockOutTimeZoneId] IS NOT NULL AND [PreviousClockOutSource] IN ('ManagerApprovedCorrection', 'AdminDirectCorrection') AND [PreviousClockOutProvider] IS NULL AND [PreviousClockOutProviderSampleUtc] IS NULL)");
        });
        builder.HasKey(change => change.AuditId);
        builder.Property(change => change.WorkDate).HasColumnType("date").IsRequired();
        builder.Property(change => change.PreviousWorkDate).HasColumnType("date").IsRequired();
        builder.Property(change => change.PreviousClockInAtUtc).HasColumnType("datetimeoffset(7)").IsRequired();
        builder.Property(change => change.PreviousClockInTimeZoneId).HasMaxLength(100).IsRequired();
        builder.Property(change => change.PreviousClockInSource).HasConversion<string>().HasMaxLength(40).IsRequired();
        builder.Property(change => change.PreviousClockInProvider).HasMaxLength(100);
        builder.Property(change => change.PreviousClockInProviderSampleUtc).HasColumnType("datetimeoffset(7)");
        builder.Property(change => change.ClockInAtUtc).HasColumnType("datetimeoffset(7)").IsRequired();
        builder.Property(change => change.ClockInTimeZoneId).HasMaxLength(100).IsRequired();
        builder.Property(change => change.ClockInSource).HasConversion<string>().HasMaxLength(40).IsRequired();
        builder.Property(change => change.ClockInProvider).HasMaxLength(100);
        builder.Property(change => change.ClockInProviderSampleUtc).HasColumnType("datetimeoffset(7)");
        builder.Property(change => change.PreviousClockOutAtUtc).HasColumnType("datetimeoffset(7)");
        builder.Property(change => change.PreviousClockOutTimeZoneId).HasMaxLength(100);
        builder.Property(change => change.PreviousClockOutSource).HasConversion<string>().HasMaxLength(40);
        builder.Property(change => change.PreviousClockOutProvider).HasMaxLength(100);
        builder.Property(change => change.PreviousClockOutProviderSampleUtc).HasColumnType("datetimeoffset(7)");
        builder.Property(change => change.ClockOutAtUtc).HasColumnType("datetimeoffset(7)");
        builder.Property(change => change.ClockOutTimeZoneId).HasMaxLength(100);
        builder.Property(change => change.ClockOutSource).HasConversion<string>().HasMaxLength(40);
        builder.Property(change => change.ClockOutProvider).HasMaxLength(100);
        builder.Property(change => change.ClockOutProviderSampleUtc).HasColumnType("datetimeoffset(7)");
        builder.Property(change => change.Reason).HasMaxLength(1000).IsRequired();
        builder.Property(change => change.ChangedAtUtc).HasColumnType("datetimeoffset(7)").IsRequired();
        builder.Property(change => change.ChangeTimeZoneId).HasMaxLength(100).IsRequired();
        builder.Property(change => change.ChangeTimeProvider).HasMaxLength(100).IsRequired();
        builder.Property(change => change.ChangeProviderSampleUtc).HasColumnType("datetimeoffset(7)").IsRequired();
        builder.HasOne(change => change.WorkShift)
            .WithMany(shift => shift.AdminChangeLogs)
            .HasForeignKey(change => new { change.CompanyId, change.WorkShiftId, change.EmployeeProfileId })
            .HasPrincipalKey(shift => new { shift.CompanyId, shift.WorkShiftId, shift.EmployeeProfileId })
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<CompanyMembership>()
            .WithMany()
            .HasForeignKey(change => new { change.CompanyId, change.ChangedByMembershipId })
            .HasPrincipalKey(membership => new { membership.CompanyId, membership.MembershipId })
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(change => new { change.CompanyId, change.EmployeeProfileId, change.WorkDate, change.ChangedAtUtc });
        builder.HasIndex(change => new { change.CompanyId, change.WorkShiftId, change.ChangedAtUtc });
        builder.HasIndex(change => new { change.CompanyId, change.ChangedByMembershipId, change.ChangedAtUtc });
    }
}
