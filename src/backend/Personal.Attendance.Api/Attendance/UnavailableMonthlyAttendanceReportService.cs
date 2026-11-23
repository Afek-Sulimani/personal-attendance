using Personal.Attendance.Application.Attendance;

namespace Personal.Attendance.Api.Attendance;

public sealed class UnavailableMonthlyAttendanceReportService : IMonthlyAttendanceReportService
{
    private static MonthlyAttendanceReportUnavailableException Error() =>
        new("SQL Server must be configured before monthly attendance can be viewed.");

    public Task<MonthlyAttendanceReportView> GetCurrentMineAsync(AttendanceActor actor, CancellationToken cancellationToken) =>
        Task.FromException<MonthlyAttendanceReportView>(Error());

    public Task<MonthlyAttendanceReportView> GetMineAsync(AttendanceActor actor, int year, int month, CancellationToken cancellationToken) =>
        Task.FromException<MonthlyAttendanceReportView>(Error());

    public Task<IReadOnlyList<MonthlyAttendanceTeamMember>> GetTeamMembersAsync(
        AttendanceActor actor,
        CancellationToken cancellationToken) =>
        Task.FromException<IReadOnlyList<MonthlyAttendanceTeamMember>>(Error());

    public Task<MonthlyAttendanceReportView> GetCurrentTeamMemberAsync(
        AttendanceActor actor,
        Guid employeeMembershipId,
        CancellationToken cancellationToken) =>
        Task.FromException<MonthlyAttendanceReportView>(Error());

    public Task<MonthlyAttendanceReportView> GetTeamMemberAsync(
        AttendanceActor actor,
        Guid employeeMembershipId,
        int year,
        int month,
        CancellationToken cancellationToken) =>
        Task.FromException<MonthlyAttendanceReportView>(Error());

}
