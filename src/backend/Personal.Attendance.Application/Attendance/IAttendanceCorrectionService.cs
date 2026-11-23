namespace Personal.Attendance.Application.Attendance;

public interface IAttendanceCorrectionService
{
    Task<Guid> SubmitAsync(
        SubmitAttendanceCorrectionCommand command,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<AttendanceCorrectionSummary>> GetPendingAsync(
        AttendanceActor actor,
        CancellationToken cancellationToken);

    Task ApproveAsync(
        AttendanceActor actor,
        Guid requestId,
        CancellationToken cancellationToken);

    Task RejectAsync(
        AttendanceActor actor,
        Guid requestId,
        string reason,
        CancellationToken cancellationToken);
}
