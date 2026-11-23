namespace Personal.Attendance.Api.Authentication;

public static class AttendanceAuthenticationDefaults
{
    public const string SchemeName = "PersonalSession";
    public const string LoginRateLimitPolicyName = "auth-login";
    public const string CompanyIdClaim = "personal:company_id";
    public const string CompanyCodeClaim = "personal:company_code";
    public const string MembershipIdClaim = "personal:membership_id";
    public const string EmployeeProfileIdClaim = "personal:employee_profile_id";
    public const string EmployeeNumberClaim = "personal:employee_number";
}
