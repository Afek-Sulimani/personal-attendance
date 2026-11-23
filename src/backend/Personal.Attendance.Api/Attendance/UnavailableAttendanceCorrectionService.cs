using Personal.Attendance.Application.Attendance;

namespace Personal.Attendance.Api.Attendance;

public sealed class UnavailableAttendanceCorrectionService : IAttendanceCorrectionService
{
    private static AttendanceCorrectionUnavailableException CreateUnavailableException() =>
        new("Configure the Attendance SQL Server connection before using attendance corrections.");

    public Task<Guid> SubmitAsync(
        SubmitAttendanceCorrectionCommand command,
        CancellationToken cancellationToken) =>
        throw CreateUnavailableException();

    public Task<IReadOnlyList<AttendanceCorrectionSummary>> GetPendingAsync(
        AttendanceActor actor,
        CancellationToken cancellationToken) =>
        throw CreateUnavailableException();

    public Task ApproveAsync(
        AttendanceActor actor,
        Guid requestId,
        CancellationToken cancellationToken) =>
        throw CreateUnavailableException();

    public Task RejectAsync(
        AttendanceActor actor,
        Guid requestId,
        string reason,
        CancellationToken cancellationToken) =>
        throw CreateUnavailableException();
}
