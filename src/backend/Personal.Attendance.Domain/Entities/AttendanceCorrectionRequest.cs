using Personal.Attendance.Domain.Enums;

namespace Personal.Attendance.Domain.Entities;

public sealed class AttendanceCorrectionRequest
{
    private AttendanceCorrectionRequest() { }

    public Guid RequestId { get; private set; }
    public Guid CompanyId { get; private set; }
    public Guid EmployeeProfileId { get; private set; }
    public Guid RequestedByMembershipId { get; private set; }
    public Guid? WorkShiftId { get; private set; }
    public Guid? DirectApproverMembershipId { get; private set; }
    public DateOnly WorkDate { get; private set; }
    public string TimeZoneId { get; private set; } = string.Empty;
    public DateTimeOffset? RequestedClockInAtUtc { get; private set; }
    public DateTimeOffset? RequestedClockOutAtUtc { get; private set; }
    public DateTimeOffset SubmittedAtUtc { get; private set; }
    public string SubmissionTimeProvider { get; private set; } = string.Empty;
    public DateTimeOffset SubmissionProviderSampleUtc { get; private set; }
    public AttendanceCorrectionStatus Status { get; private set; }
    public Guid? ReviewedByMembershipId { get; private set; }
    public DateTimeOffset? ReviewedAtUtc { get; private set; }
    public string? RejectionReason { get; private set; }
    public byte[] RowVersion { get; private set; } = [];

    public static AttendanceCorrectionRequest Create(
        Guid companyId,
        Guid employeeProfileId,
        Guid requestedByMembershipId,
        Guid? workShiftId,
        Guid? directApproverMembershipId,
        DateOnly workDate,
        string timeZoneId,
        DateTimeOffset? requestedClockInAtUtc,
        DateTimeOffset? requestedClockOutAtUtc,
        DateTimeOffset submittedAtUtc,
        string submissionTimeProvider,
        DateTimeOffset submissionProviderSampleUtc)
    {
        if (companyId == Guid.Empty) throw new ArgumentException("Company ID is required.", nameof(companyId));
        if (employeeProfileId == Guid.Empty) throw new ArgumentException("Employee profile ID is required.", nameof(employeeProfileId));
        if (requestedByMembershipId == Guid.Empty) throw new ArgumentException("Requester membership ID is required.", nameof(requestedByMembershipId));
        if (workShiftId == Guid.Empty) throw new ArgumentException("Work shift ID cannot be empty.", nameof(workShiftId));
        if (directApproverMembershipId == Guid.Empty) throw new ArgumentException("Approver membership ID cannot be empty.", nameof(directApproverMembershipId));
        ValidateUtc(submittedAtUtc, nameof(submittedAtUtc));
        ValidateUtc(submissionProviderSampleUtc, nameof(submissionProviderSampleUtc));
        ArgumentException.ThrowIfNullOrWhiteSpace(submissionTimeProvider);
        ArgumentException.ThrowIfNullOrWhiteSpace(timeZoneId);
        if (timeZoneId.Trim().Length > 100) throw new ArgumentOutOfRangeException(nameof(timeZoneId));
        _ = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId.Trim());

        if (requestedClockInAtUtc is null && requestedClockOutAtUtc is null)
        {
            throw new ArgumentException("At least one corrected punch time is required.");
        }
        if (requestedClockInAtUtc is { } clockIn)
        {
            ValidateUtc(clockIn, nameof(requestedClockInAtUtc));
        }
        if (requestedClockOutAtUtc is { } clockOut)
        {
            ValidateUtc(clockOut, nameof(requestedClockOutAtUtc));
        }
        if (requestedClockInAtUtc is { } start && requestedClockOutAtUtc is { } end && end < start)
        {
            throw new ArgumentException("Corrected clock-out cannot precede corrected clock-in.");
        }
        if (workShiftId is null && requestedClockInAtUtc is null)
        {
            throw new ArgumentException("A new historical shift requires a clock-in time.");
        }
        if (workShiftId is not null && (requestedClockInAtUtc is not null || requestedClockOutAtUtc is null))
        {
            throw new ArgumentException("A correction for an existing shift must contain only a clock-out time.");
        }

        return new AttendanceCorrectionRequest
        {
            RequestId = Guid.NewGuid(),
            CompanyId = companyId,
            EmployeeProfileId = employeeProfileId,
            RequestedByMembershipId = requestedByMembershipId,
            WorkShiftId = workShiftId,
            DirectApproverMembershipId = directApproverMembershipId,
            WorkDate = workDate,
            TimeZoneId = timeZoneId.Trim(),
            RequestedClockInAtUtc = requestedClockInAtUtc,
            RequestedClockOutAtUtc = requestedClockOutAtUtc,
            SubmittedAtUtc = submittedAtUtc,
            SubmissionTimeProvider = submissionTimeProvider.Trim(),
            SubmissionProviderSampleUtc = submissionProviderSampleUtc,
            Status = AttendanceCorrectionStatus.PendingApproval
        };
    }

    public void Approve(Guid reviewerMembershipId, DateTimeOffset reviewedAtUtc)
    {
        EnsurePending();
        ValidateReviewer(reviewerMembershipId, reviewedAtUtc);
        Status = AttendanceCorrectionStatus.Approved;
        ReviewedByMembershipId = reviewerMembershipId;
        ReviewedAtUtc = reviewedAtUtc;
        RejectionReason = null;
    }

    public void SetApprovedWorkShiftId(Guid workShiftId)
    {
        if (Status != AttendanceCorrectionStatus.Approved)
        {
            throw new InvalidOperationException("Only an approved correction can reference the resulting shift.");
        }
        if (workShiftId == Guid.Empty) throw new ArgumentException("Work shift ID is required.", nameof(workShiftId));
        WorkShiftId = workShiftId;
    }

    public void Reject(Guid reviewerMembershipId, DateTimeOffset reviewedAtUtc, string reason)
    {
        EnsurePending();
        ValidateReviewer(reviewerMembershipId, reviewedAtUtc);
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        var trimmedReason = reason.Trim();
        if (trimmedReason.Length > 1000) throw new ArgumentOutOfRangeException(nameof(reason));

        Status = AttendanceCorrectionStatus.Rejected;
        ReviewedByMembershipId = reviewerMembershipId;
        ReviewedAtUtc = reviewedAtUtc;
        RejectionReason = trimmedReason;
    }

    public void Supersede()
    {
        EnsurePending();
        Status = AttendanceCorrectionStatus.Superseded;
        ReviewedByMembershipId = null;
        ReviewedAtUtc = null;
        RejectionReason = null;
    }

    private void EnsurePending()
    {
        if (Status != AttendanceCorrectionStatus.PendingApproval)
        {
            throw new InvalidOperationException("Only pending correction requests can be reviewed.");
        }
    }

    private static void ValidateReviewer(Guid membershipId, DateTimeOffset reviewedAtUtc)
    {
        if (membershipId == Guid.Empty) throw new ArgumentException("Reviewer membership ID is required.", nameof(membershipId));
        ValidateUtc(reviewedAtUtc, nameof(reviewedAtUtc));
    }

    private static void ValidateUtc(DateTimeOffset value, string parameterName)
    {
        if (value.Offset != TimeSpan.Zero)
        {
            throw new ArgumentException("The value must use UTC offset zero.", parameterName);
        }
    }
}
