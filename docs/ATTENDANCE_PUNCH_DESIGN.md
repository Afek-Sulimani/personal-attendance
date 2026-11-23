# Attendance Workspace Design

**Status:** The employee attendance calendar, Payroll settings and monthly workday setup, correction review, and Admin attendance audit page are implemented over SQL Server.

## Scope

The feature provides role-aware React pages for company settings, attendance recording, employee schedules, correction review, employee invitations, and Admin edits to existing attendance shifts. ASP.NET Core owns authorization, time-zone handling, business rules, and database writes.

The calendar groups shifts by their Clock In work date. An overnight shift is displayed only on its Clock In date. Punch values are presented in the time zone saved on each individual event.

## Company settings and work schedules

- Admin or Payroll configures the payroll-cycle start day, company time zone, standard work start/end times, and default weekdays.
- A new company defaults to `Asia/Jerusalem`; no payroll start day, standard hours, or workdays are assumed until an Admin or Payroll user saves them.
- Company defaults apply to every employee who does not have a saved monthly schedule.
- Payroll can save exact expected work dates per employee for each calendar month. A saved monthly schedule overrides company defaults for that employee/month.
- Clock In availability is checked on the server against the configured schedule even if React disables the button.
- The payroll start day selects report boundaries and determines post-cutoff attention for pending corrections. Employee and manager tables are read-only payroll-period views. Whole-period approval, scheduled cutoff jobs, and manager email delivery are not implemented; see [the email workflow decision](../DECISIONS.md#original-email-workflow-and-final-scope).

## Live time integrity

- A live Clock In or Clock Out calls `ICurrentAttendanceClock.GetCurrentTimeForAttendanceAsync` with the company's configured time-zone ID. If no fresh synchronized sample exists, the attendance clock requests a fresh sample from configured external providers and fails closed when all sources are unavailable.
- A hosted service refreshes registered zone snapshots hourly. The synchronized clock advances each valid provider sample using monotonic elapsed time between refreshes.
- Live event instants are stored in UTC at minute precision, truncated down. Source, provider, provider sample time, and event time-zone ID are retained.
- Company settings default to `Asia/Jerusalem`. Each shift stores `WorkDate` derived from its Clock In instant in its saved Clock In zone. Clock Out can occur on the next date and uses its own saved time-zone ID, while the shift remains grouped under its Clock In date.
- Changing a company's time zone affects future events and current calendar boundaries. It does not rewrite historical punch time-zone IDs or work dates.
- Browser time, server wall-clock time, and database time are never used as the recorded time of a live punch.

## Employee correction policy

- A historical or missed punch is submitted as a correction request; it is not recorded as a live external punch.
- A request stores its selected time zone, proposed UTC values, and the external provider sample used for the request submission timestamp.
- If correcting an existing open shift, the input uses that shift's stored Clock In time zone. A correction for a date with no shift uses the company's current configured time zone.
- A missing past shift requires both Clock In and Clock Out. Today's missing Clock In may be requested without a Clock Out. An old open shift can receive a Clock Out-only request. A completed shift cannot be submitted for a second correction in this feature.
- The employee can set Clock Out on the following local day for an overnight shift. Clock In and Clock Out must be within 24 hours. Submission timing is not yet limited by a deadline.
- Ambiguous or nonexistent local times during a daylight-saving transition are rejected.
- Direct managers review requests. For an initial company representative without a manager, another active HR or Payroll membership in the same company can review it. Self-approval is rejected. Rejection requires a reason.
- Approval/rejection, request state, and an approved shift change are saved in one SQL transaction.

## Admin attendance edits

- An active company Admin can directly correct any existing shift, including their own. A second approval is not required.
- The feature only edits a row already present in `ATTENDANCE_WORK_SHIFTS`; it cannot create a shift for a day with no record.
- Admin supplies a mandatory reason. The edit and a before/after audit row are saved in one SQL transaction; a SQL trigger rejects updates and deletes to audit rows.
- Each existing punch keeps its saved time zone. The Admin page converts the submitted local date/time using the time zone saved for that punch.
- Future values, invalid daylight-saving local times, out-of-order values, duplicate employee/work-date shifts, and shifts longer than 24 hours are rejected.
- The audit records actor membership, work date before/after, event values and event-source provenance before/after, reason, time zone, and the external provider/sample used for the audit timestamp.

## HTTP routes

| Method | Route | Access | Purpose |
|---|---|---|---|
| `GET` | `/api/attendance/current` | Authenticated employee | Return the employee's current open shift, if any. |
| `GET` | `/api/attendance/calendar?year={year}&month={month}` | Authenticated employee | Return company-local today, monthly workdays, event values with saved time zones, and day statuses. Omit both parameters for the current company-local month. |
| `POST` | `/api/attendance/clock-in` | Authenticated employee + antiforgery | Open a shift using the synchronized external company clock. |
| `POST` | `/api/attendance/clock-out` | Authenticated employee + antiforgery | Close the current shift using the synchronized external company clock. |
| `POST` | `/api/attendance/corrections` | Authenticated employee + antiforgery | Submit a manager-reviewed missed/late punch correction. |
| `GET` | `/api/attendance/corrections/pending` | Assigned manager or eligible company reviewer | List pending requests the current member may review. |
| `POST` | `/api/attendance/corrections/{requestId}/approve` | Authorized reviewer + antiforgery | Approve and apply the correction in one transaction. |
| `POST` | `/api/attendance/corrections/{requestId}/reject` | Authorized reviewer + antiforgery | Reject with a required reason. |
| `GET` | `/api/payroll/company-settings` | Admin or Payroll | Read company-level defaults. |
| `PUT` | `/api/payroll/company-settings` | Admin or Payroll + antiforgery | Save company settings and default weekdays. |
| `GET` | `/api/payroll/employees` | Admin or Payroll | List employee profiles in the company. |
| `GET` | `/api/payroll/employees/{membershipId}/work-days?year={year}&month={month}` | Admin or Payroll | Read a monthly employee schedule, with company-default weekdays when no monthly override exists. |
| `PUT` | `/api/payroll/employees/{membershipId}/work-days?year={year}&month={month}` | Admin or Payroll + antiforgery | Replace the monthly employee work dates. |
| `GET` | `/api/employee-invitations/managers` | Admin, HR, or Payroll | List eligible direct managers in the current company. |
| `POST` | `/api/employee-invitations` | Admin, HR, or Payroll + antiforgery | Create a 48-hour invitation with a selected manager and roles allowed for the caller. |
| `GET` | `/api/admin/attendance?employeeMembershipId=&year=&month=` | Admin | Read existing employee shifts and audit records that affect the requested month, including a work date moved into or out of it. |
| `PUT` | `/api/admin/attendance/{workShiftId}` | Admin + antiforgery | Correct an existing shift with a required reason and write its audit snapshot. |

The employee correction request body uses company-local selected times such as `09:05:00.0000000` and a boolean to mark a following-day Clock Out:

```json
{
  "workDate": "2026-11-19",
  "clockInLocalTime": "09:05:00.0000000",
  "clockOutLocalTime": "17:30:00.0000000",
  "clockOutNextDay": false
}
```

The API returns `409 Conflict` for an invalid state transition, non-working date, duplicate shift or pending correction, out-of-order time, over-24-hour duration, or future proposed punch. It returns `503 Service Unavailable` if SQL Server or a valid required external time sample is unavailable.

## Calendar statuses

| Status | Dot | Meaning |
|---|---|---|
| `Complete` | Green | Clock In and Clock Out are recorded. |
| `Open` | Red | Clock In is recorded; the shift remains open. |
| `Missing` | Orange | A past expected work date has no attendance record. |
| `CorrectionPending` | Orange | A correction awaits manager review. |
| `CorrectionRejected` | Orange | The latest correction was rejected. |
| `Future` | Gray | The date is later than the company-time API date. |
| `NonWorking` | Gray | The date is not expected by the monthly schedule or company defaults. |
| `NoPunchToday` | No dot | No punch has been recorded today. |
| `ScheduleNotConfigured` | No dot | No monthly schedule or company default workweek exists yet. |

## Main code locations

| Location | Responsibility |
|---|---|
| `Infrastructure/Attendance/AttendanceService.cs` | Live punches, workday validation, and employee calendar projection. |
| `Infrastructure/Attendance/AttendanceCorrectionService.cs` | Correction validation, manager authorization, and approval transaction. |
| `Infrastructure/Attendance/AdminAttendanceService.cs` | Admin authority, existing-shift correction, external audit time, and before/after audit persistence. |
| `Infrastructure/Payroll/CompanyPayrollSettingsService.cs` | Company policy and default weekday persistence. |
| `Infrastructure/Payroll/PayrollWorkScheduleService.cs` | Employee list and monthly workday overrides. |
| `Api/Time/TimeSampleCoordinator.cs` and `Infrastructure/Time/SynchronizedClock.cs` | Per-time-zone external sample refresh and monotonic snapshot calculation. |
| `Api/Controllers/AttendanceController.cs` | Calendar, current shift, Clock In, and Clock Out routes. |
| `Api/Controllers/CompanyPayrollSettingsController.cs` | Company payroll/time-zone settings routes. |
| `Api/Controllers/AdminAttendanceController.cs` | Admin month-read and existing-shift edit routes. |
| `frontend/personal-web/src/pages/AttendancePage.tsx` | Employee calendar, live punches, and correction form. |
| `frontend/personal-web/src/pages/CompanyPayrollSettingsPage.tsx` | Company settings form. |
| `frontend/personal-web/src/pages/PayrollWorkDaysPage.tsx` | Monthly employee workday override. |
| `frontend/personal-web/src/pages/AdminAttendancePage.tsx` | Admin shift editor and audit history. |

## Local database setup

Configure `ConnectionStrings:Attendance`, apply all EF Core migrations, and run the API and Vite server using [DATABASE_SETUP.md](DATABASE_SETUP.md). The application does not apply migrations automatically. No seed data is part of this feature.
