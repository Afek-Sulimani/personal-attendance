namespace Personal.Attendance.Infrastructure.Invitations;

internal static class InvitationLinkPolicy
{
    public static readonly TimeSpan Lifetime = TimeSpan.FromHours(48);
}
