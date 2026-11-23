namespace Personal.Attendance.Application.Invitations;

public enum EmployeeInvitationOperationStatus
{
    Succeeded,
    InvalidOrExpired,
    Forbidden,
    Conflict,
    ExistingAccountMustSignIn,
    PasswordRequired,
    InvalidPassword,
    ManagerRequired,
    Unavailable
}
