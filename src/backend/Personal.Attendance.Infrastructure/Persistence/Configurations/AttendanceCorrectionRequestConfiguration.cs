using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Personal.Attendance.Domain.Entities;

namespace Personal.Attendance.Infrastructure.Persistence.Configurations;

internal sealed class AttendanceCorrectionRequestConfiguration : IEntityTypeConfiguration<AttendanceCorrectionRequest>
{
    public void Configure(EntityTypeBuilder<AttendanceCorrectionRequest> builder)
    {
        builder.ToTable("ATTENDANCE_ATTENDANCE_CORRECTION_REQUESTS", table =>
        {
            table.HasCheckConstraint(
                "CK_ATTENDANCE_ATTENDANCE_CORRECTION_REQUESTS_TIME_ORDER",
                "[RequestedClockOutAtUtc] IS NULL OR [RequestedClockInAtUtc] IS NULL OR [RequestedClockOutAtUtc] >= [RequestedClockInAtUtc]");
            table.HasCheckConstraint(
                "CK_ATTENDANCE_ATTENDANCE_CORRECTION_REQUESTS_MAX_DURATION",
                $"[RequestedClockOutAtUtc] IS NULL OR [RequestedClockInAtUtc] IS NULL OR [RequestedClockOutAtUtc] <= DATEADD(hour, {WorkShift.MaximumDurationHours}, [RequestedClockInAtUtc])");
        });
        builder.HasKey(request => request.RequestId);
        builder.HasAlternateKey(request => new { request.CompanyId, request.RequestId });
        builder.Property(request => request.WorkDate).HasColumnType("date").IsRequired();
        builder.Property(request => request.TimeZoneId).HasMaxLength(100).IsRequired();
        builder.Property(request => request.RequestedClockInAtUtc).HasColumnType("datetimeoffset(7)");
        builder.Property(request => request.RequestedClockOutAtUtc).HasColumnType("datetimeoffset(7)");
        builder.Property(request => request.SubmittedAtUtc).HasColumnType("datetimeoffset(7)").IsRequired();
        builder.Property(request => request.SubmissionTimeProvider).HasMaxLength(100).IsRequired();
        builder.Property(request => request.SubmissionProviderSampleUtc).HasColumnType("datetimeoffset(7)").IsRequired();
        builder.Property(request => request.Status).HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.Property(request => request.ReviewedAtUtc).HasColumnType("datetimeoffset(7)");
        builder.Property(request => request.RejectionReason).HasMaxLength(1000);
        builder.Property(request => request.RowVersion).IsRowVersion();
        builder.HasOne<EmployeeProfile>()
            .WithMany()
            .HasForeignKey(request => new { request.CompanyId, request.EmployeeProfileId })
            .HasPrincipalKey(profile => new { profile.CompanyId, profile.EmployeeProfileId })
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<WorkShift>()
            .WithMany()
            .HasForeignKey(request => new { request.CompanyId, request.WorkShiftId, request.EmployeeProfileId })
            .HasPrincipalKey(shift => new { shift.CompanyId, shift.WorkShiftId, shift.EmployeeProfileId })
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<CompanyMembership>()
            .WithMany()
            .HasForeignKey(request => new { request.CompanyId, request.RequestedByMembershipId })
            .HasPrincipalKey(membership => new { membership.CompanyId, membership.MembershipId })
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<CompanyMembership>()
            .WithMany()
            .HasForeignKey(request => new { request.CompanyId, request.DirectApproverMembershipId })
            .HasPrincipalKey(membership => new { membership.CompanyId, membership.MembershipId })
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<CompanyMembership>()
            .WithMany()
            .HasForeignKey(request => new { request.CompanyId, request.ReviewedByMembershipId })
            .HasPrincipalKey(membership => new { membership.CompanyId, membership.MembershipId })
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(request => new { request.CompanyId, request.EmployeeProfileId, request.WorkDate });
        builder.HasIndex(request => new { request.CompanyId, request.DirectApproverMembershipId, request.Status });
        builder.HasIndex(request => new { request.CompanyId, request.Status, request.SubmittedAtUtc });
        builder.HasIndex(request => new { request.CompanyId, request.EmployeeProfileId, request.WorkDate })
            .IsUnique()
            .HasDatabaseName("IX_ATTENDANCE_ATTENDANCE_CORRECTION_REQUESTS_CompanyId_EmployeeProfileId_WorkDate")
            .HasFilter("[Status] = 'PendingApproval'");
    }
}
