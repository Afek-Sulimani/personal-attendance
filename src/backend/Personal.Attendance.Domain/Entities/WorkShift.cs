using Personal.Attendance.Domain.Enums;

namespace Personal.Attendance.Domain.Entities;

public sealed class WorkShift
{
    // Live shifts longer than this must be fixed through the manager-reviewed correction flow.
    public const int MaximumDurationHours = 24;

    private WorkShift() { }

    // Store punch instants in UTC and keep the zone/provider that explains each local display and source.
    public Guid WorkShiftId { get; private set; }
    public Guid CompanyId { get; private set; }
    public Guid EmployeeProfileId { get; private set; }
    public DateOnly WorkDate { get; private set; }
    public DateTimeOffset ClockInAtUtc { get; private set; }
    public string ClockInTimeZoneId { get; private set; } = string.Empty;
    public AttendanceTimeSource ClockInSource { get; private set; }
    public string? ClockInProvider { get; private set; }
    public DateTimeOffset? ClockInProviderSampleUtc { get; private set; }
    public DateTimeOffset? ClockOutAtUtc { get; private set; }
    public string? ClockOutTimeZoneId { get; private set; }
    public AttendanceTimeSource? ClockOutSource { get; private set; }
    public string? ClockOutProvider { get; private set; }
    public DateTimeOffset? ClockOutProviderSampleUtc { get; private set; }
    public byte[] RowVersion { get; private set; } = [];

    public EmployeeProfile EmployeeProfile { get; private set; } = null!;
    public ICollection<AdminAttendanceChangeLog> AdminChangeLogs { get; } = new List<AdminAttendanceChangeLog>();
    public bool IsOpen => ClockOutAtUtc is null;

    // Preserve a compatibility overload for callers that use the system's default Israel zone.
    public static WorkShift ClockIn(
        Guid companyId,
        Guid employeeProfileId,
        DateTimeOffset clockInAtUtc,
        string provider,
        DateTimeOffset providerSampleUtc) =>
        ClockIn(companyId, employeeProfileId, clockInAtUtc, provider, providerSampleUtc,
            "Asia/Jerusalem", GetLocalDate(clockInAtUtc, "Asia/Jerusalem"));

    // Create a live shift only from an external sample and a work date matching that sample in the saved zone.
    public static WorkShift ClockIn(
        Guid companyId,
        Guid employeeProfileId,
        DateTimeOffset clockInAtUtc,
        string provider,
        DateTimeOffset providerSampleUtc,
        string timeZoneId,
        DateOnly workDate)
    {
        if (companyId == Guid.Empty) throw new ArgumentException("Company ID is required.", nameof(companyId));
        if (employeeProfileId == Guid.Empty) throw new ArgumentException("Employee profile ID is required.", nameof(employeeProfileId));
        // Reject non-UTC or incomplete provider provenance before constructing a persisted attendance record.
        ValidateExternalTime(clockInAtUtc, provider, providerSampleUtc);
        ValidateTimeZoneId(timeZoneId, nameof(timeZoneId));
        ValidateWorkDate(clockInAtUtc, timeZoneId, workDate, nameof(workDate));

        // Store minute precision, the company's local work date, and the exact source metadata used for the punch.
        return new WorkShift
        {
            WorkShiftId = Guid.NewGuid(),
            CompanyId = companyId,
            EmployeeProfileId = employeeProfileId,
            WorkDate = workDate,
            ClockInAtUtc = TruncateToMinute(clockInAtUtc),
            ClockInTimeZoneId = timeZoneId.Trim(),
            ClockInSource = AttendanceTimeSource.ExternalApi,
            ClockInProvider = provider.Trim(),
            ClockInProviderSampleUtc = providerSampleUtc
        };
    }

    // Keep the legacy correction overload on the default zone while newer callers pass the stored company zone.
    public static WorkShift CreateFromApprovedCorrection(
        Guid companyId,
        Guid employeeProfileId,
        DateTimeOffset clockInAtUtc,
        DateTimeOffset? clockOutAtUtc) =>
        CreateFromApprovedCorrection(
            companyId,
            employeeProfileId,
            GetLocalDate(clockInAtUtc, "Asia/Jerusalem"),
            clockInAtUtc,
            clockOutAtUtc,
            "Asia/Jerusalem");

    // Build a shift from manager-approved historical values without claiming an external provider supplied them.
    public static WorkShift CreateFromApprovedCorrection(
        Guid companyId,
        Guid employeeProfileId,
        DateOnly workDate,
        DateTimeOffset clockInAtUtc,
        DateTimeOffset? clockOutAtUtc,
        string timeZoneId)
    {
        if (companyId == Guid.Empty) throw new ArgumentException("Company ID is required.", nameof(companyId));
        if (employeeProfileId == Guid.Empty) throw new ArgumentException("Employee profile ID is required.", nameof(employeeProfileId));
        ValidateCorrectionTime(clockInAtUtc, nameof(clockInAtUtc));
        ValidateTimeZoneId(timeZoneId, nameof(timeZoneId));
        ValidateWorkDate(clockInAtUtc, timeZoneId, workDate, nameof(workDate));
        // Validate an optional exit only when this correction closes the shift.
        if (clockOutAtUtc is { } clockOut)
        {
            ValidateCorrectionTime(clockOut, nameof(clockOutAtUtc));
            ValidateDuration(clockInAtUtc, clockOut, nameof(clockOutAtUtc));
        }

        // Mark manually corrected values as approved-correction sources and leave provider fields empty.
        return new WorkShift
        {
            WorkShiftId = Guid.NewGuid(),
            CompanyId = companyId,
            EmployeeProfileId = employeeProfileId,
            WorkDate = workDate,
            ClockInAtUtc = TruncateToMinute(clockInAtUtc),
            ClockInTimeZoneId = timeZoneId.Trim(),
            ClockInSource = AttendanceTimeSource.ManagerApprovedCorrection,
            ClockOutAtUtc = clockOutAtUtc is null ? null : TruncateToMinute(clockOutAtUtc.Value),
            ClockOutTimeZoneId = clockOutAtUtc is null ? null : timeZoneId.Trim(),
            ClockOutSource = clockOutAtUtc is null ? null : AttendanceTimeSource.ManagerApprovedCorrection
        };
    }

    // Create a direct Admin correction while preserving its distinct source for later audit and reporting.
    public static WorkShift CreateFromAdminCorrection(
        Guid companyId,
        Guid employeeProfileId,
        DateOnly workDate,
        DateTimeOffset clockInAtUtc,
        DateTimeOffset? clockOutAtUtc,
        string timeZoneId)
    {
        if (companyId == Guid.Empty) throw new ArgumentException("Company ID is required.", nameof(companyId));
        if (employeeProfileId == Guid.Empty) throw new ArgumentException("Employee profile ID is required.", nameof(employeeProfileId));
        ValidateCorrectionTime(clockInAtUtc, nameof(clockInAtUtc));
        ValidateTimeZoneId(timeZoneId, nameof(timeZoneId));
        ValidateWorkDate(clockInAtUtc, timeZoneId, workDate, nameof(workDate));
        // Apply the same ordering and 24-hour checks to an optional Admin-supplied exit.
        if (clockOutAtUtc is { } clockOut)
        {
            ValidateCorrectionTime(clockOut, nameof(clockOutAtUtc));
            ValidateDuration(clockInAtUtc, clockOut, nameof(clockOutAtUtc));
        }

        // Admin-entered values have no provider sample, so persist their direct-correction source only.
        return new WorkShift
        {
            WorkShiftId = Guid.NewGuid(),
            CompanyId = companyId,
            EmployeeProfileId = employeeProfileId,
            WorkDate = workDate,
            ClockInAtUtc = TruncateToMinute(clockInAtUtc),
            ClockInTimeZoneId = timeZoneId.Trim(),
            ClockInSource = AttendanceTimeSource.AdminDirectCorrection,
            ClockOutAtUtc = clockOutAtUtc is null ? null : TruncateToMinute(clockOutAtUtc.Value),
            ClockOutTimeZoneId = clockOutAtUtc is null ? null : timeZoneId.Trim(),
            ClockOutSource = clockOutAtUtc is null ? null : AttendanceTimeSource.AdminDirectCorrection
        };
    }

    // Update an existing shift and clear provider provenance only for punch values Admin actually changes.
    public void ApplyAdminCorrection(
        DateTimeOffset clockInAtUtc,
        DateTimeOffset? clockOutAtUtc,
        DateOnly correctedWorkDate)
    {
        ValidateCorrectionTime(clockInAtUtc, nameof(clockInAtUtc));
        if (clockOutAtUtc is { } clockOut)
        {
            ValidateCorrectionTime(clockOut, nameof(clockOutAtUtc));
            ValidateDuration(clockInAtUtc, clockOut, nameof(clockOutAtUtc));
        }

        var correctedClockIn = TruncateToMinute(clockInAtUtc);
        ValidateWorkDate(correctedClockIn, ClockInTimeZoneId, correctedWorkDate, nameof(correctedWorkDate));
        WorkDate = correctedWorkDate;
        // Retain the original external source if the entry instant is unchanged; otherwise mark it as Admin-edited.
        if (ClockInAtUtc != correctedClockIn)
        {
            ClockInAtUtc = correctedClockIn;
            ClockInSource = AttendanceTimeSource.AdminDirectCorrection;
            ClockInProvider = null;
            ClockInProviderSampleUtc = null;
        }

        DateTimeOffset? correctedClockOut = clockOutAtUtc is null
            ? null
            : TruncateToMinute(clockOutAtUtc.Value);
        // Apply the same source reset to Clock Out, including when Admin removes an existing exit.
        if (ClockOutAtUtc != correctedClockOut)
        {
            ClockOutAtUtc = correctedClockOut;
            ClockOutSource = correctedClockOut is null ? null : AttendanceTimeSource.AdminDirectCorrection;
            ClockOutProvider = null;
            ClockOutProviderSampleUtc = null;
        }
    }

    // Reuse the saved Clock In zone when an older caller does not pass an explicit exit zone.
    public void ClockOut(
        DateTimeOffset clockOutAtUtc,
        string provider,
        DateTimeOffset providerSampleUtc) =>
        ClockOut(clockOutAtUtc, provider, providerSampleUtc, ClockInTimeZoneId);

    // Close an open shift using a live external sample while retaining the zone used for this exit.
    public void ClockOut(
        DateTimeOffset clockOutAtUtc,
        string provider,
        DateTimeOffset providerSampleUtc,
        string timeZoneId)
    {
        // Prevent a second live exit from rewriting a shift that is already complete.
        if (!IsOpen) throw new InvalidOperationException("A closed shift cannot be clocked out again.");
        ValidateExternalTime(clockOutAtUtc, provider, providerSampleUtc);
        ValidateTimeZoneId(timeZoneId, nameof(timeZoneId));
        ValidateDuration(ClockInAtUtc, clockOutAtUtc, nameof(clockOutAtUtc));

        // Save the exit in UTC at minute precision and retain its external provider provenance.
        ClockOutAtUtc = TruncateToMinute(clockOutAtUtc);
        ClockOutTimeZoneId = timeZoneId.Trim();
        ClockOutSource = AttendanceTimeSource.ExternalApi;
        ClockOutProvider = provider.Trim();
        ClockOutProviderSampleUtc = providerSampleUtc;
    }

    // Route the simple overload through the saved Clock In zone to keep overnight shift dates consistent.
    public void SetClockOutFromApprovedCorrection(DateTimeOffset clockOutAtUtc) =>
        SetClockOutFromApprovedCorrection(clockOutAtUtc, ClockInTimeZoneId);

    // Apply a manager-approved exit to an open shift and clear external-provider fields for this manual value.
    public void SetClockOutFromApprovedCorrection(DateTimeOffset clockOutAtUtc, string timeZoneId)
    {
        if (!IsOpen) throw new InvalidOperationException("Only an open shift can receive a corrected clock-out.");
        ValidateCorrectionTime(clockOutAtUtc, nameof(clockOutAtUtc));
        ValidateTimeZoneId(timeZoneId, nameof(timeZoneId));
        ValidateDuration(ClockInAtUtc, clockOutAtUtc, nameof(clockOutAtUtc));

        // Store the approved exit with its zone and source, but no false provider attribution.
        ClockOutAtUtc = TruncateToMinute(clockOutAtUtc);
        ClockOutTimeZoneId = timeZoneId.Trim();
        ClockOutSource = AttendanceTimeSource.ManagerApprovedCorrection;
        ClockOutProvider = null;
        ClockOutProviderSampleUtc = null;
    }

    // Require zero-offset UTC instants and a provider name for every live external punch.
    private static void ValidateExternalTime(DateTimeOffset timestampUtc, string provider, DateTimeOffset providerSampleUtc)
    {
        if (timestampUtc.Offset != TimeSpan.Zero) throw new ArgumentException("Attendance timestamp must use UTC offset zero.", nameof(timestampUtc));
        if (providerSampleUtc.Offset != TimeSpan.Zero) throw new ArgumentException("Provider sample must use UTC offset zero.", nameof(providerSampleUtc));
        ArgumentException.ThrowIfNullOrWhiteSpace(provider);
    }

    // Drop seconds and fractions consistently so comparisons and reports use minute precision.
    private static DateTimeOffset TruncateToMinute(DateTimeOffset value) =>
        new(value.Year, value.Month, value.Day, value.Hour, value.Minute, 0, TimeSpan.Zero);

    // Normalize both instants before applying the same ordering and maximum-length rule used by SQL Server.
    public static bool IsDurationWithinMaximum(DateTimeOffset clockInAtUtc, DateTimeOffset clockOutAtUtc)
    {
        var normalizedClockIn = TruncateToMinute(clockInAtUtc.ToUniversalTime());
        var normalizedClockOut = TruncateToMinute(clockOutAtUtc.ToUniversalTime());
        return normalizedClockOut >= normalizedClockIn &&
               normalizedClockOut <= normalizedClockIn.AddHours(MaximumDurationHours);
    }

    // Turn the shared duration predicate into a domain exception for entity creation and update methods.
    private static void ValidateDuration(
        DateTimeOffset clockInAtUtc,
        DateTimeOffset clockOutAtUtc,
        string parameterName)
    {
        if (!IsDurationWithinMaximum(clockInAtUtc, clockOutAtUtc))
        {
            throw new ArgumentOutOfRangeException(
                parameterName,
                $"Clock-out must be between clock-in and {MaximumDurationHours} hours after clock-in.");
        }
    }

    private static void ValidateCorrectionTime(DateTimeOffset timestampUtc, string parameterName)
    {
        if (timestampUtc.Offset != TimeSpan.Zero)
        {
            throw new ArgumentException("Corrected attendance time must use UTC offset zero.", parameterName);
        }
    }

    // Derive a work date in the punch's zone so a shift belongs to its local Clock In day.
    private static DateOnly GetLocalDate(DateTimeOffset timestampUtc, string timeZoneId)
    {
        var timeZone = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
        return DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(timestampUtc, timeZone).DateTime);
    }

    // Validate and bound a zone ID before storing it in a column with a fixed maximum length.
    private static void ValidateTimeZoneId(string timeZoneId, string parameterName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(timeZoneId);
        if (timeZoneId.Trim().Length > 100)
        {
            throw new ArgumentOutOfRangeException(parameterName, "Time zone IDs cannot exceed 100 characters.");
        }
        // Ask the platform to reject unsupported IDs now instead of failing on a later display conversion.
        _ = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId.Trim());
    }

    // Require WorkDate to equal the stored local date for the Clock In instant in its recorded zone.
    private static void ValidateWorkDate(
        DateTimeOffset timestampUtc,
        string timeZoneId,
        DateOnly workDate,
        string parameterName)
    {
        if (GetLocalDate(timestampUtc, timeZoneId) != workDate)
        {
            throw new ArgumentException("The work date must match the punch time in its stored time zone.", parameterName);
        }
    }
}
