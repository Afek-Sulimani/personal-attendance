namespace Personal.Attendance.Infrastructure.Invitations;

public sealed class InvitationLinkOptions
{
    public const string SectionName = "InvitationLinks";

    public string PublicBaseUrl { get; set; } = string.Empty;
}
