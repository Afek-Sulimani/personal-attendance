# API endpoint reference

This inventory is derived from the API controllers under `src/backend/Personal.Attendance.Api/Controllers`. Protected routes use the authenticated company membership from the server-side session; callers cannot select another company by sending a company ID. State-changing cookie-authenticated requests require the antiforgery token returned by `GET /api/auth/antiforgery`.

## Health, clock, and authentication

| Method | Endpoint | Access | What it returns or changes |
|---|---|---|---|
| GET | `/api/health/live` | Public | Basic process liveness response. |
| GET | `/api/clock/current?timeZoneId=` | Public | Current synchronized clock reading in the requested supported time zone; defaults to `Asia/Jerusalem`. |
| GET | `/api/time-sources/{sourceId}?timeZoneId=` | Public | Direct diagnostic reading from a named configured time provider. |
| GET | `/api/auth/antiforgery` | Public | Creates/returns the antiforgery request token and cookie pair. |
| POST | `/api/auth/login` | Public + antiforgery | Checks company name or ID, email, and password; starts an authenticated company session. Rate limited. |
| GET | `/api/auth/me` | Authenticated | Returns the active user's identity, company membership, employee number, and roles. |
| POST | `/api/auth/logout` | Authenticated + antiforgery | Ends the current authenticated session. |

## Company registration, invitations, and employee profile

| Method | Endpoint | Access | What it returns or changes |
|---|---|---|---|
| POST | `/api/companies/registration` | Public + antiforgery | Creates a company, generates its five-digit company ID, and creates the first Admin invitation. No email is sent. |
| GET | `/api/employee-invitations/managers` | Admin, HR, or Payroll | Lists eligible managers in the current company for the invitation form. |
| POST | `/api/employee-invitations` | Admin, HR, or Payroll + antiforgery | Creates a company membership invitation, assigns selected roles and manager, and returns a temporary activation URL. |
| POST | `/api/employee-invitations/validate` | Public | Validates an invitation token and returns the invited account and company details. |
| POST | `/api/employee-invitations/accept` | Public + antiforgery | Accepts a valid invitation and creates/activates the account and membership. |
| GET | `/api/profile` | Authenticated | Returns the signed-in account, company memberships, employee identifiers, roles, and compensation details. |
| POST | `/api/profile/password` | Authenticated + antiforgery | Changes the password after checking the current password, then revokes the user's active sessions. |
| GET | `/api/profile/company-employees` | Admin, HR, or Payroll | Lists company employee profiles and configured compensation details. |
| PUT | `/api/profile/company-employees/{employeeMembershipId}/compensation` | Admin, HR, or Payroll + antiforgery | Sets an employee's `Global` or `Hourly` pay type and optional numeric hourly rate. |

## Company settings and work schedules

| Method | Endpoint | Access | What it returns or changes |
|---|---|---|---|
| GET | `/api/payroll/company-settings` | Admin or Payroll | Returns the company's payroll-period start day, time zone, standard hours, and default work weekdays. |
| PUT | `/api/payroll/company-settings` | Admin or Payroll + antiforgery | Saves those company-wide payroll and schedule settings. |
| GET | `/api/payroll/employees` | Admin or Payroll | Lists company employees available for a workday override. |
| GET | `/api/payroll/employees/{employeeMembershipId}/work-days?year=&month=` | Admin or Payroll | Reads one employee's workday override for the selected calendar month. |
| PUT | `/api/payroll/employees/{employeeMembershipId}/work-days?year=&month=` | Admin or Payroll + antiforgery | Saves the employee's working dates for that calendar month. |

## Attendance and correction review

| Method | Endpoint | Access | What it returns or changes |
|---|---|---|---|
| GET | `/api/attendance/calendar?year=&month=` | Authenticated employee | Returns the employee's selected calendar-month days, workday status, punches, correction status, and the current payroll-period dates. |
| GET | `/api/attendance/current` | Authenticated employee | Returns current company-local date/time and the employee's open-shift state. |
| POST | `/api/attendance/clock-in` | Authenticated employee + antiforgery | Starts a shift using the synchronized company clock. |
| POST | `/api/attendance/clock-out` | Authenticated employee + antiforgery | Ends the employee's open shift using the synchronized company clock. |
| POST | `/api/attendance/corrections` | Authenticated employee + antiforgery | Creates or replaces the pending correction for a work date; the latest request is the one reviewed. |
| GET | `/api/attendance/corrections/pending` | Eligible direct manager, Admin, HR, or Payroll reviewer | Lists pending correction requests the reviewer may decide, including requested times and cutoff attention state. |
| POST | `/api/attendance/corrections/{requestId}/approve` | Authorized reviewer + antiforgery | Approves a correction and applies it to attendance. |
| POST | `/api/attendance/corrections/{requestId}/reject` | Authorized reviewer + antiforgery | Rejects a correction; a reason is required and recorded. |

## Monthly attendance views

These are read-only views. They do not have a report submission or monthly-report approval operation. `year` and `month` select the month containing the **start date** of a payroll period: for example, with a start day of 16, October selects October 16–November 15. The current routes select the currently active payroll period. Each expected workday has a compact row; non-workdays are omitted. Night-shift duration belongs to its Clock In date. Pending correction hours count in `totalWorkedMinutes` and are also returned separately in `pendingCorrectionMinutes`.

| Method | Endpoint | Access | What it returns or changes |
|---|---|---|---|
| GET | `/api/monthly-attendance-reports/mine/current` | Authenticated employee | Returns the employee's current payroll-period rows, daily punches, statuses, and total hours including pending corrections. |
| GET | `/api/monthly-attendance-reports/mine?year=&month=` | Authenticated employee | Returns the employee's payroll period whose start date falls in the selected year and month. |
| GET | `/api/monthly-attendance-reports/team/members` | Team Lead or Admin, HR, or Payroll | Lists employees within the reviewer's reporting scope. |
| GET | `/api/monthly-attendance-reports/team/members/{employeeMembershipId}/current` | Authorized manager or company reviewer | Returns the selected employee's current payroll-period rows and totals. |
| GET | `/api/monthly-attendance-reports/team/members/{employeeMembershipId}?year=&month=` | Authorized manager or company reviewer | Returns the selected employee's payroll period whose start date falls in the selected year and month. |

## Admin attendance changes

| Method | Endpoint | Access | What it returns or changes |
|---|---|---|---|
| GET | `/api/admin/attendance?employeeMembershipId=&year=&month=` | Admin | Lists the selected employee's existing shifts and audit history for the selected calendar month. |
| PUT | `/api/admin/attendance/{workShiftId}` | Admin + antiforgery | Edits an existing shift with a required reason and records the before/after state and actor. It cannot create a shift. |

## Monthly view response semantics

- `periodStart` and `periodEnd` are the configured company payroll-period range, not calendar-month boundaries.
- `totalWorkedMinutes` includes existing reported shifts plus complete pending correction durations. `pendingCorrectionMinutes` is the pending subset of that total.
- `days` includes scheduled workdays and a date with a pending correction. A pending row shows the requested time and requested duration with `PendingApproval` status.
- A shift crossing midnight remains attached to its original Clock In work date, and its full duration is counted there.
- Missing scheduled workdays remain `MissingWorkday`/“No report”; no leave deduction is made.

## Not provided by this API

The application does not send email, calculate wages or deductions, maintain leave balances, or require submission/approval of a monthly report. Managers review individual correction requests. See [ARCHITECTURE.md](ARCHITECTURE.md) and [TESTING.md](TESTING.md) for implementation and verification details.
