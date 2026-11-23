# Personal Attendance Database Design

**Status:** Current local SQL Server schema design, including employee compensation settings and monthly attendance reports.

## Relationship diagram

```mermaid
erDiagram
    ATTENDANCE_USERS ||--o{ ATTENDANCE_COMPANY_MEMBERSHIPS : belongs_to
    ATTENDANCE_COMPANIES ||--o{ ATTENDANCE_COMPANY_MEMBERSHIPS : has
    ATTENDANCE_COMPANY_MEMBERSHIPS ||--o{ ATTENDANCE_MEMBERSHIP_ROLES : assigned
    ATTENDANCE_COMPANY_MEMBERSHIPS ||--o| ATTENDANCE_EMPLOYEE_PROFILES : describes
    ATTENDANCE_COMPANY_MEMBERSHIPS ||--o{ ATTENDANCE_COMPANY_MEMBERSHIPS : manages
    ATTENDANCE_COMPANIES ||--o| ATTENDANCE_COMPANY_PAYROLL_SETTINGS : configures
    ATTENDANCE_COMPANY_PAYROLL_SETTINGS ||--|{ ATTENDANCE_COMPANY_DEFAULT_WORK_DAYS : defaults_to
    ATTENDANCE_COMPANY_MEMBERSHIPS ||--o{ ATTENDANCE_COMPANY_PAYROLL_SETTINGS : configured_by
    ATTENDANCE_EMPLOYEE_PROFILES ||--o{ ATTENDANCE_WORK_SHIFTS : records
    ATTENDANCE_EMPLOYEE_PROFILES ||--o{ ATTENDANCE_EMPLOYEE_MONTHLY_WORK_SCHEDULES : scheduled
    ATTENDANCE_EMPLOYEE_MONTHLY_WORK_SCHEDULES ||--o{ ATTENDANCE_EMPLOYEE_MONTHLY_WORK_DAYS : contains
    ATTENDANCE_EMPLOYEE_PROFILES ||--o{ ATTENDANCE_ATTENDANCE_CORRECTION_REQUESTS : requests
    ATTENDANCE_EMPLOYEE_PROFILES ||--o{ ATTENDANCE_MONTHLY_ATTENDANCE_REPORTS : reports
    ATTENDANCE_COMPANY_MEMBERSHIPS ||--o{ ATTENDANCE_MONTHLY_ATTENDANCE_REPORTS : approves
    ATTENDANCE_COMPANY_MEMBERSHIPS ||--o{ ATTENDANCE_ATTENDANCE_CORRECTION_REQUESTS : requests_or_reviews
    ATTENDANCE_WORK_SHIFTS o|--o{ ATTENDANCE_ATTENDANCE_CORRECTION_REQUESTS : may_be_corrected
    ATTENDANCE_WORK_SHIFTS ||--o{ ATTENDANCE_ADMIN_ATTENDANCE_CHANGE_LOGS : audited
    ATTENDANCE_COMPANY_MEMBERSHIPS ||--o{ ATTENDANCE_ADMIN_ATTENDANCE_CHANGE_LOGS : changed_by
    ATTENDANCE_COMPANIES ||--o{ ATTENDANCE_EMPLOYEE_INVITATIONS : owns
    ATTENDANCE_COMPANY_MEMBERSHIPS ||--o{ ATTENDANCE_EMPLOYEE_INVITATIONS : creates
    ATTENDANCE_EMPLOYEE_INVITATIONS }o--o| ATTENDANCE_USERS : accepted_by

    ATTENDANCE_USERS {
        GUID Id PK
        string Email
        string NormalizedEmail UK
        string UserName
        string PasswordHash
    }
    ATTENDANCE_COMPANIES {
        GUID CompanyId PK
        string CompanyCode UK
        string NormalizedCompanyCode UK
        string Name
        bool IsActive
        datetimeoffset CreatedAtUtc
    }
    ATTENDANCE_COMPANY_MEMBERSHIPS {
        GUID MembershipId PK
        GUID CompanyId FK
        GUID UserId FK
        GUID ManagerMembershipId FK
        bool IsActive
        datetimeoffset CreatedAtUtc
        datetimeoffset DeactivatedAtUtc
    }
    ATTENDANCE_MEMBERSHIP_ROLES {
        GUID MembershipId PK
        string Role PK
    }
    ATTENDANCE_EMPLOYEE_PROFILES {
        GUID EmployeeProfileId PK
        GUID CompanyId FK
        GUID MembershipId FK
        string DisplayName
        string EmployeeNumber
        string CompensationType
        decimal HourlyRate NULL
        datetimeoffset CreatedAtUtc
        datetimeoffset UpdatedAtUtc
    }
    ATTENDANCE_COMPANY_PAYROLL_SETTINGS {
        GUID CompanyId PK_FK
        int PayrollPeriodStartDay
        string TimeZoneId
        time DefaultWorkStartTime
        time DefaultWorkEndTime
        GUID ConfiguredByMembershipId FK
        datetimeoffset CreatedAtUtc
        datetimeoffset UpdatedAtUtc
    }
    ATTENDANCE_COMPANY_DEFAULT_WORK_DAYS {
        GUID CompanyId PK_FK
        int DayOfWeek PK
    }
    ATTENDANCE_WORK_SHIFTS {
        GUID WorkShiftId PK
        GUID CompanyId FK
        GUID EmployeeProfileId FK
        date WorkDate
        datetimeoffset ClockInAtUtc
        string ClockInTimeZoneId
        string ClockInSource
        string ClockInProvider
        datetimeoffset ClockInProviderSampleUtc
        datetimeoffset ClockOutAtUtc
        string ClockOutTimeZoneId
        string ClockOutSource
        string ClockOutProvider
        datetimeoffset ClockOutProviderSampleUtc
        rowversion RowVersion
    }
    ATTENDANCE_EMPLOYEE_MONTHLY_WORK_SCHEDULES {
        GUID ScheduleId PK
        GUID CompanyId FK
        GUID EmployeeProfileId FK
        int Year
        int Month
        GUID ConfiguredByMembershipId FK
        datetimeoffset CreatedAtUtc
        datetimeoffset UpdatedAtUtc
    }
    ATTENDANCE_EMPLOYEE_MONTHLY_WORK_DAYS {
        GUID WorkDayId PK
        GUID CompanyId FK
        GUID ScheduleId FK
        date WorkDate
    }
    ATTENDANCE_ATTENDANCE_CORRECTION_REQUESTS {
        GUID RequestId PK
        GUID CompanyId FK
        GUID EmployeeProfileId FK
        GUID RequestedByMembershipId FK
        GUID WorkShiftId FK
        GUID DirectApproverMembershipId FK
        date WorkDate
        string TimeZoneId
        datetimeoffset RequestedClockInAtUtc
        datetimeoffset RequestedClockOutAtUtc
        datetimeoffset SubmittedAtUtc
        string SubmissionTimeProvider
        datetimeoffset SubmissionProviderSampleUtc
        string Status
        GUID ReviewedByMembershipId FK
        datetimeoffset ReviewedAtUtc
        string RejectionReason
        rowversion RowVersion
    }
    ATTENDANCE_MONTHLY_ATTENDANCE_REPORTS {
        GUID ReportId PK
        GUID CompanyId FK
        GUID EmployeeProfileId FK
        GUID DirectApproverMembershipId FK
        date PeriodStart
        date PeriodEnd
        string TimeZoneId
        string Status
        datetimeoffset SubmittedAtUtc
        GUID ReviewedByMembershipId FK
        datetimeoffset ReviewedAtUtc
        string DecisionReason
        rowversion RowVersion
    }
    ATTENDANCE_ADMIN_ATTENDANCE_CHANGE_LOGS {
        GUID AuditId PK
        GUID CompanyId FK
        GUID EmployeeProfileId FK
        GUID WorkShiftId FK
        GUID ChangedByMembershipId FK
        date PreviousWorkDate
        date WorkDate
        datetimeoffset PreviousClockInAtUtc
        string PreviousClockInTimeZoneId
        string PreviousClockInSource
        string PreviousClockInProvider
        datetimeoffset PreviousClockInProviderSampleUtc
        datetimeoffset ClockInAtUtc
        string ClockInTimeZoneId
        string ClockInSource
        string ClockInProvider
        datetimeoffset ClockInProviderSampleUtc
        datetimeoffset PreviousClockOutAtUtc
        string PreviousClockOutTimeZoneId
        string PreviousClockOutSource
        string PreviousClockOutProvider
        datetimeoffset PreviousClockOutProviderSampleUtc
        datetimeoffset ClockOutAtUtc
        string ClockOutTimeZoneId
        string ClockOutSource
        string ClockOutProvider
        datetimeoffset ClockOutProviderSampleUtc
        string Reason
        datetimeoffset ChangedAtUtc
        string ChangeTimeZoneId
        string ChangeTimeProvider
        datetimeoffset ChangeProviderSampleUtc
    }
    ATTENDANCE_EMPLOYEE_INVITATIONS {
        GUID InvitationId PK
        GUID CompanyId FK
        GUID CreatedByMembershipId FK
        GUID ManagerMembershipId FK
        string InvitedEmail
        string NormalizedEmail
        string DisplayName
        string EmployeeNumber
        string AssignedRoles
        char TokenHash UK
        datetimeoffset CreatedAtUtc
        datetimeoffset ExpiresAtUtc
        datetimeoffset AcceptedAtUtc
        GUID AcceptedUserId FK
        datetimeoffset RevokedAtUtc
        rowversion RowVersion
    }
```

ASP.NET Core Identity also creates `ATTENDANCE_ROLES`, `ATTENDANCE_USER_ROLES`, `ATTENDANCE_USER_CLAIMS`, `ATTENDANCE_USER_LOGINS`, `ATTENDANCE_ROLE_CLAIMS`, and `ATTENDANCE_USER_TOKENS`.

## Product tables

| Table | What it represents | Why it exists |
|---|---|---|
| `ATTENDANCE_COMPANIES` | A tenant company and its five-digit login code. | Company membership and settings are isolated by company. |
| `ATTENDANCE_COMPANY_MEMBERSHIPS` | A user's participation in one company and direct-manager link. | One global user can join multiple companies; role and manager relationships belong to one company. |
| `ATTENDANCE_MEMBERSHIP_ROLES` | The set of roles on one membership. | One person can combine roles such as HR and Payroll. The first representative receives Admin. |
| `ATTENDANCE_EMPLOYEE_PROFILES` | Company-specific display name, employee number, and optional compensation type/rate. | Employee details and compensation vary by company, while email and username belong to the global sign-in account. |
| `ATTENDANCE_COMPANY_PAYROLL_SETTINGS` | Company-level payroll start day, time zone, standard work hours, and settings editor. | Admin or Payroll configures a company's shared attendance rules in one row. |
| `ATTENDANCE_COMPANY_DEFAULT_WORK_DAYS` | One default weekday per row for the company. | This is a normalized child collection with a company foreign key and unique `(CompanyId, DayOfWeek)` key. It avoids storing a list of days in one text field and lets SQL enforce valid one-row-per-day structure. |
| `ATTENDANCE_WORK_SHIFTS` | One shift row opened by Clock In and closed by Clock Out. | It stores UTC instants, the work date, the time-zone ID for each punch, and the event source/provider provenance. |
| `ATTENDANCE_EMPLOYEE_MONTHLY_WORK_SCHEDULES` | One employee's schedule for one calendar month. | Payroll can override company default weekdays with exact dates per employee/month. |
| `ATTENDANCE_EMPLOYEE_MONTHLY_WORK_DAYS` | One expected work date in a monthly schedule. | Attendance validates these dates on the server. If a monthly schedule exists, it overrides the company default weekdays. |
| `ATTENDANCE_ATTENDANCE_CORRECTION_REQUESTS` | A proposed employee correction and its review lifecycle. | It keeps the proposal, request time zone, external submission-time provenance, manager decision, and any rejection reason separate from live punches. |
| `ATTENDANCE_MONTHLY_ATTENDANCE_REPORTS` | Legacy table from an earlier monthly submission design. | The current application does not read or write this table; manager review applies to individual correction requests only. It remains in migration history to avoid silently dropping existing database data. |
| `ATTENDANCE_ADMIN_ATTENDANCE_CHANGE_LOGS` | A before/after audit snapshot for an Admin edit to an existing shift. | It records the actor, reason, before/after values, original punch zones and provenance, and external time sample for the edit timestamp. A SQL trigger rejects updates and deletes to audit rows. |
| `ATTENDANCE_EMPLOYEE_INVITATIONS` | One pending, accepted, expired, or revoked account invitation. | It keeps invitation details, role assignments, token hash, and lifecycle in one row. The activation URL is shared manually; the app sends no email. |

## Data rules represented in the schema and services

- Product IDs, user IDs, and role IDs use GUIDs.
- `CompensationType` is nullable until configured and stores `Global` or `Hourly`. `HourlyRate` is nullable for a global salary and required as a positive number with up to two decimal places for an hourly employee. No currency is stored or displayed.
- A company membership references one company and one global user. Only one membership per company/user pair is allowed.
- Manager and employee-profile relationships use company-scoped foreign keys to prevent cross-company links.
- Each company has at most one payroll-settings row. A company may have default workdays only after payroll settings are created.
- A saved employee monthly schedule is unique per company, employee, year, and month. Each scheduled date is unique within the schedule.
- Company time-zone configuration defaults to `Asia/Jerusalem`. Each Clock In and Clock Out stores the time zone used for that punch. Historical punches keep their original zones if the company setting changes later.
- `WorkDate` is the local date of Clock In in the punch's stored time zone. An overnight Clock Out does not move the shift to the next calendar date.
- A closed shift cannot end before Clock In or exceed 24 elapsed hours. The database permits at most one open shift per employee/company; the service also prevents a second shift on the same work date.
- Live events use external time samples and store provider provenance. Employee and Admin corrections are labeled as corrections and do not pretend their edited event time came from an external provider.
- An Admin can correct an existing shift, including their own, only with a reason. The shift update and audit row are saved in one transaction. This feature does not let Admin create a shift on a date with no shift.
- The audit row includes the previous and updated event values, sources, per-event time zones, providers, and sample timestamps. The audit timestamp itself is based on the external company-time sample.
- One pending correction per employee/work date is allowed. Approval applies a shift change and request status in one SQL transaction.
- Monthly attendance is currently calculated as a read-only view over shifts, work schedules, and pending corrections. It is not submitted or approved as a monthly report. The legacy `ATTENDANCE_MONTHLY_ATTENDANCE_REPORTS` table and migration are retained for schema history but are unused by current application flows.
- Live event values are truncated down to minute precision. Seconds are not rounded.
- Invitation tokens are stored as unique hashes; raw tokens are returned only inside the activation link. Links expire after 48 hours and can be accepted once.
- Row versions protect shifts, invitations, and correction requests from conflicting concurrent updates.

## Boundaries

The schema does not include calculated payroll amounts, tax, deductions, leave balances, or email delivery. Employee compensation type and an optional numeric hourly rate are stored on the company-specific employee profile; this is configuration only and does not calculate wages. The company payroll-period start day selects the employee and manager report window. Red review attention is limited to pending correction requests after the end of their payroll period; no monthly-report approval or scheduled cutoff job exists. Employee workday override schedules are configured by calendar month. See [docs/DATABASE_SETUP.md](docs/DATABASE_SETUP.md) for local SQL Server setup and migration commands.
