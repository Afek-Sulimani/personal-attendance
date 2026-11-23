# User flow scenarios for Playwright

## Test data and scope

SQL-backed tests create a uniquely named company for each run in a disposable database.
`npm run seed:local` prepares one active account per role and prints local test credentials.
All credentials are synthetic; never run this setup against production data.
This catalogue includes planned acceptance cases as well as implemented tests. The `.spec.ts`
files and test results are the source of truth for current automated coverage.

## Shared onboarding and approval flow

| ID | Action | Expected result |
| --- | --- | --- |
| SETUP-01 | Register a company and its first representative | A five-digit company ID and single-use activation link valid for 48 hours are returned. |
| SETUP-02 | Activate the representative with a password | The account receives Admin only and employee number 00001; reuse of the invitation fails. |
| SETUP-03 | Sign in using company name or ID, email and password | Admin navigation and the correct company profile appear. |
| SETUP-04 | Invite Payroll, HR and a team lead | Each invitation preserves its selected roles and manager; employee numbers are allocated on activation; duplicate submission is prevented. |
| SETUP-05 | Configure payroll period, time zone, hours and workdays | Settings persist and attendance becomes available on eligible workdays. |
| SETUP-06 | Invite an employee and select a direct team lead | The activated employee appears in the manager's team; HR/Payroll cannot assign Admin. |
| SETUP-07 | Request a correction for a missing workday | Requested times appear as Awaiting approval and contribute to total and pending hours. |
| SETUP-08 | Approve the employee correction | The shift is created or updated and reports show approved hours. |

## Admin

| ID | Action | Expected result |
| --- | --- | --- |
| ADM-01 | Sign in, navigate the workspace and open My profile | Authorized pages open; the account displays Admin only. |
| ADM-02 | Invite individual or combined company roles | Selected roles persist without automatically adding Employee to professional roles. |
| ADM-03 | Search managers by name or employee number | Selection persists; a direct manager is required. |
| ADM-04 | Set Global pay, then Hourly pay with a rate | The employee profile reflects the configuration; Hourly requires a valid rate. |
| ADM-05 | Update company payroll, time zone, hours and weekdays | New attendance uses current settings; historical punches retain their original zones. |
| ADM-06 | Save an employee-specific monthly work schedule | Only that employee's calendar and report change. |
| ADM-07 | Edit an existing shift with a reason | The change persists with actor, reason and before/after audit values. |
| ADM-08 | Edit without a reason or attempt to create a shift through Admin editing | Invalid operations are rejected; only existing shifts may be edited. |
| ADM-09 | Browse personal and employee reports across payroll periods | Tables show scheduled dates, reported hours and pending subtotals. |
| ADM-10 | Change password and sign out | Password change revokes sessions; sign-out invalidates the current session. |

## Payroll

| ID | Action | Expected result |
| --- | --- | --- |
| PAY-01 | Inspect navigation after sign-in | Company settings, schedules, people, pay, corrections and reports are available; Admin attendance editing is denied. |
| PAY-02 | Configure and update company defaults | Settings persist; non-workdays do not allow Clock In. |
| PAY-03 | Save an employee's monthly schedule override | Only the selected employee and month are affected. |
| PAY-04 | Invite Employee, Team lead, HR or Payroll | Invitations are created; Admin cannot be assigned. |
| PAY-05 | Configure Global or Hourly compensation | Valid values persist and invalid or missing hourly rates are rejected. |
| PAY-06 | Approve or reject an eligible correction | The queue and employee report update; rejection requires a reason. |
| PAY-07 | Review employee reported, missing and pending hours | Company authorization and payroll-period boundaries apply; no email is sent. |
| PAY-08 | Attempt Admin-only operations through direct URLs | The API denies access. |

## HR

| ID | Action | Expected result |
| --- | --- | --- |
| HR-01 | Inspect role navigation | People, pay, eligible corrections and reports are available; company settings, schedules and Admin editing are denied. |
| HR-02 | Invite a non-Admin member and select a manager | Activation link preserves selected roles; Admin assignment is denied. |
| HR-03 | Update employee compensation | Pay type and hourly rate appear in the employee profile. |
| HR-04 | Review and decide an eligible correction | Request state, attendance and reports update consistently. |
| HR-05 | Browse employee reports and payroll periods | Scheduled dates, daily hours, totals and pending hours are displayed. |
| HR-06 | Attempt company-settings or Admin editing endpoints | The API rejects unauthorized requests. |

## Team lead

| ID | Action | Expected result |
| --- | --- | --- |
| LEAD-01 | Inspect role navigation | Personal attendance, team reports, corrections and profile are available; company administration is denied. |
| LEAD-02 | Select a direct report and browse periods | Authorized employee data is visible; unrelated employees are inaccessible. |
| LEAD-03 | Open an empty corrections queue | An accessible empty state appears without a permission error. |
| LEAD-04 | Approve a direct report's correction | The request leaves the queue and approved hours appear in the report. |
| LEAD-05 | Reject first without, then with, a reason | Empty reasons are rejected; valid rejection is saved and visible to the employee. |
| LEAD-06 | Review a prior-period pending correction | Attention indicators appear on navigation and the employee group; requested times use the saved zone. |
| LEAD-07 | Attempt an unrelated employee's approval or compensation update | The API denies access. |

## Employee

| ID | Action | Expected result |
| --- | --- | --- |
| EMP-01 | Open attendance, monthly attendance and profile | Only personal navigation appears; memberships, contact details and compensation are visible. |
| EMP-02 | Clock In and Clock Out on today's eligible workday | Actions reflect shift state; duplicate entry and exit without entry are rejected. |
| EMP-03 | Select future, non-working or out-of-period dates | Live punches remain hidden or disabled as appropriate. |
| EMP-04 | Request missing attendance including an overnight shift | The request awaits review and cannot exceed 24 hours. |
| EMP-05 | Replace a pending correction for the same day | Only the newest request is actionable by the reviewer. |
| EMP-06 | Read the report before approval | Requested times and duration appear as Awaiting approval, with total and pending subtotals. |
| EMP-07 | Read the report after approval or rejection | Approved hours persist; rejected proposals are removed from totals and can be resubmitted. |
| EMP-08 | Close an overnight shift within 24 hours | The entire duration belongs to the entry date. |
| EMP-09 | Change password and sign out | Invalid password input fails; successful change requires another login. |
| EMP-10 | Open Admin attendance through a direct URL | Protected API operations reject the employee. |
| EMP-11 | Open company pay management | Another employee's compensation management is inaccessible. |
| EMP-12 | Clock In after today's correction was rejected | Live entry is available when no shift or pending request blocks it. |
| EMP-13 | Return to the tab or wait for periodic refresh | The page reloads authoritative state from the server. |
| EMP-14 | Cross company-local midnight | Yesterday no longer exposes today's live punch actions. |

## Cross-role and security acceptance cases

| ID | Action | Expected result |
| --- | --- | --- |
| CROSS-01 | Sign in with incorrect company, email or password | No session is created; errors do not disclose the invalid field. |
| CROSS-02 | Reuse an invitation, open an expired invitation or use another account | Invalid activation is rejected without attaching the wrong identity. |
| CROSS-03 | Access two memberships belonging to different companies | Employee data, shifts and settings remain company-scoped. |
| CROSS-04 | Change company zone after recording attendance | Old punches preserve their zones and new punches use the updated configuration. |
| CROSS-05 | Refresh or submit actions twice rapidly | Duplicate creation or decisions are prevented and results persist. |
| CROSS-06 | Browse a mid-month payroll period across month/year boundaries | Dates start at cutoff and end before the next cutoff; only scheduled workdays appear. |
| CROSS-07 | Use mobile navigation | Role links and controls remain accessible; choosing a page closes the menu. |
| CROSS-08 | Submit a delayed sign-in request with reduced-motion preferences | Loading feedback prevents duplicate actions and recovers from failure; reduced motion remains static. |

## Personal edition browser flows

| ID | Action | Expected result |
| --- | --- | --- |
| PERSONAL-CSV | Download the selected payroll-period report | CSV includes pending times, zone, durations, totals and formula-safe fields. |
| PERSONAL-GOOGLE-ON | Sign in when Google OAuth is configured | A company is required before OAuth handoff; the company identifier is preserved. |
| PERSONAL-GOOGLE-OFF | Open login without Google OAuth credentials | The unavailable Google sign-in action is not displayed. |

Real Google consent and callback require locally configured OAuth credentials; browser
handoff tests do not assert successful authentication with Google itself.
