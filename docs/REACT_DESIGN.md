# React Product Design and API Map

**Status:** The client-rendered React routes and Playwright scenarios for sign-in, activation, registration, attendance, monthly reports, staff management, payroll setup, employee profile, and compensation are implemented locally.

## Product shell

Use the existing React + TypeScript client-rendered SPA. A private attendance and staff-management product does not need search-engine rendering; the API remains the authority for identity, permissions, company scope, and all attendance times.

Use a shared responsive application shell with:

- A company label and current user's display name.
- Navigation items derived from the roles returned by `GET /api/auth/me`.
- A clear employee area and role-specific management areas. A person may have more than one role, such as HR and TeamLead.
- Loading, empty, error, and access-denied states for every data-backed page.
- A single API client for JSON, session cookies, antiforgery tokens, and consistent error handling.

The Vite development proxy already forwards `/api` to ASP.NET Core. Production should serve the SPA and API under the same site where possible so the protected session cookie and antiforgery flow remain straightforward.

## Product pages

The current application exposes these implemented product routes, plus shared loading, error, and not-found states.

| Route | Audience | Main responsibility |
|---|---|---|
| `/login` | Signed-out users | Sign in with company name or ID, email, and password. |
| `/activate` | Invited employee | Validate a one-time link and set the account password. |
| `/register-company` | New company representative | Create a company and issue a 48-hour activation link; the first representative receives Admin only. |
| `/attendance` | All employees | View current shift state, Clock In/Clock Out, calendar status, and correction requests. |
| `/reports/monthly` | All employees | View scheduled workdays in the current or selected payroll period, daily hours, exceptions, and total hours. |
| `/team/monthly-reports` | Admin, TeamLead, HR, Payroll | Choose an employee and view a compact payroll-period workday table; TeamLead sees direct reports, company roles see company employees. |
| `/team/attendance-corrections` | TeamLead, HR, Payroll | Review employee correction requests with the configured approval rules. |
| `/admin/people` | Admin, HR, Payroll | Create employee invitations and choose permitted roles and a direct manager; only Admin may assign Admin. |
| `/admin/employee-profiles` | Admin, HR, Payroll | Configure company-specific Global or Hourly compensation and a numeric hourly amount. |
| `/admin/attendance` | Admin | Correct an existing shift with a reason and inspect the audit history. |
| `/payroll/company-settings` | Admin, Payroll | Configure payroll cycle, company time zone, standard hours, and default weekdays. |
| `/payroll/work-days` | Admin, Payroll | Configure an employee's work days for a calendar month. |
| `/profile` | All employees | View email, username, active company memberships, employee numbers, roles, pay type, and hourly amount; change password. |

Navigation is filtered by roles for usability, while each API endpoint independently enforces authorization and company scope. A person with multiple roles sees the combined set of permitted pages.

## Page behavior and API dependencies

### Login and session

1. `GET /api/auth/antiforgery` stores the antiforgery cookie and returns a request token.
2. `POST /api/auth/login` submits company name or ID, email, password, and `X-CSRF-TOKEN`.
3. `GET /api/auth/me` supplies the current user's roles and selected company membership after login or page refresh.
4. `POST /api/auth/logout` ends the session and requires `X-CSRF-TOKEN`.

The browser keeps the session in the protected cookie. It must not store passwords, authentication tickets, or invitation tokens in `localStorage`.

### Invitation activation

The invite URL places the token in the URL fragment: `/activate#token=...`. The activation page reads it into memory, immediately removes the fragment from browser history, and posts it in JSON. The page calls `POST /api/employee-invitations/validate`, then `POST /api/employee-invitations/accept` with the chosen password. Both mutating browser flows must use the antiforgery token.

The API returns the link and supports validation and acceptance. The React route reads the token from the URL fragment, removes it from the address bar, and keeps it in memory while the employee sets a password or signs in to an existing account.

### Company registration

The `/register-company` page calls `POST /api/companies/registration` with the company name and first company representative's name and email. The server generates a five-digit company ID and creates the company and its initial 48-hour invitation in one database transaction. The first representative receives the Admin role only. No email is sent; the registration result displays the company ID, representative email, a short account-activation label, and a Copy button that copies the full activation URL. Production onboarding must require system approval instead of public company creation.

### Employee profile and compensation

The `/profile` page calls `GET /api/profile` for global account details and all active company memberships. Each membership supplies its own company code, employee number, company roles, compensation type, and hourly amount. Password changes call `POST /api/profile/password` with the current and new passwords and antiforgery protection. After the API accepts the change, it invalidates all in-memory sessions for that user and signs out the current browser.

The `/admin/employee-profiles` page calls `GET /api/profile/company-employees` and saves a selected employee's pay type using `PUT /api/profile/company-employees/{employeeMembershipId}/compensation`. Admin, HR, and Payroll can configure `Global` or `Hourly`; only Hourly accepts a positive numeric rate to two decimal places. No currency is collected or stored, and the system does not calculate payroll.

### Employee attendance

The `/attendance` page uses `GET /api/attendance/current` to determine whether a shift is open. Clock In and Clock Out call the existing POST routes and display `OccurredAtIsrael` returned by the server. The browser never sends a timestamp, employee ID, or company ID and never substitutes `Date.now()` for an attendance time.

The monthly calendar and live punch flows use the existing attendance API. Backdated employee punches remain correction requests and require the existing approval workflow; the browser does not supply the timestamp for live Clock In or Clock Out.

### Monthly attendance overview

The `/reports/monthly` page loads the current or selected payroll period based on the company's configured period start day. The `/team/monthly-reports` page loads an authorized employee list and the selected payroll period. For a cycle starting on the 16th, selecting October means October 16 through November 15. Both views show scheduled workdays in one compact table, daily punch times, missing workdays, and total reported duration. Pending correction durations appear in the total and are shown separately as awaiting approval. Non-workdays are omitted. A complete shift, including an overnight shift, belongs to its Clock In date.

Monthly attendance no longer requires employee submission or manager approval. Backdated corrections remain separate requests and continue to require manager review. The product does not yet automatically classify missing days as leave or calculate deductions or leave balances.

### HR and Payroll

The `/admin/people` page uses the existing `POST /api/employee-invitations` endpoint to create an invitation and display a copyable link once. Admin, HR, and Payroll can assign supported roles; only Admin can assign the Admin role. No automatic email action is shown.

The `/payroll/company-settings` page configures the company's payroll cycle and default work weekdays. The `/payroll/work-days` page configures an employee's active work days for a calendar month. Dates are displayed according to those settings; inactive days are visually de-emphasized and the current company-local day is marked.

## API inventory

### Existing endpoints

| Method | Endpoint | Used by | State |
|---|---|---|---|
| `GET` | `/api/auth/antiforgery` | Login, logout, invitation create/accept | Implemented |
| `POST` | `/api/auth/login` | Login page | Implemented |
| `GET` | `/api/auth/me` | App shell and route permissions | Implemented |
| `POST` | `/api/auth/logout` | App shell | Implemented |
| `GET` | `/api/profile` | Profile page | Implemented |
| `POST` | `/api/profile/password` | Profile page | Implemented; revokes the user's active in-memory sessions |
| `GET` | `/api/profile/company-employees` | Compensation page | Implemented for Admin, HR, and Payroll |
| `PUT` | `/api/profile/company-employees/{employeeMembershipId}/compensation` | Compensation page | Implemented; stores company-specific type and numeric rate |
| `GET` | `/api/attendance/current` | Attendance page | Implemented |
| `POST` | `/api/attendance/clock-in` | Attendance page | Implemented |
| `POST` | `/api/attendance/clock-out` | Attendance page | Implemented |
| `POST` | `/api/employee-invitations` | Staff page | Implemented; returns a 48-hour, one-time URL |
| `POST` | `/api/employee-invitations/validate` | Activation page | Implemented |
| `POST` | `/api/employee-invitations/accept` | Activation page | Implemented |
| `POST` | `/api/companies/registration` | Company registration | Implemented; creates a company and first Admin invitation |
| `GET` | `/api/monthly-attendance-reports/mine/current` | Employee monthly attendance | Current payroll period, workday rows, daily time, and total hours including pending corrections |
| `GET` | `/api/monthly-attendance-reports/mine?year=&month=` | Employee monthly attendance | Payroll period whose start date is in the selected year/month |
| `GET` | `/api/monthly-attendance-reports/team/members` | Team monthly attendance | Direct reports for TeamLead; company employees for Admin, HR, and Payroll |
| `GET` | `/api/monthly-attendance-reports/team/members/{membershipId}/current` | Team monthly attendance | Current payroll period for an authorized employee |
| `GET` | `/api/monthly-attendance-reports/team/members/{membershipId}?year=&month=` | Team monthly attendance | Payroll period starting in the selected month for an authorized employee |
| `GET` | `/api/clock/current` | Developer diagnostics | Implemented; not a substitute for punch response time |
| `GET` | `/api/time-sources/{sourceId}` | Developer diagnostics | Implemented; not a product attendance source for the browser |
| `GET` | `/api/health/live` | Runtime diagnostics | Implemented |

### Deferred product behavior

- Monthly attendance is read-only; saved shifts and the configured schedule drive both employee and manager views.
- Payroll calculations, tax, deductions, leave balances, and automatic leave classification are not implemented.
- The employee-invitation page creates invitations; a separate searchable employee directory is not part of this page yet.
- Playwright role flows are implemented; [the E2E README](../src/frontend/personal-web/e2e/README.md) records coverage and remaining gaps.

Any future endpoint must treat company and membership identifiers as resource selectors only. The server must derive the acting user and active company from the authenticated session and verify object-level permissions.

## React implementation boundaries

- Keep the app as a client-rendered SPA. Use route-level page components and feature folders for authentication, attendance, staff, manager review, and payroll settings.
- Keep route-level pages and feature-specific API request/response types separate from display components.
- Use one API client that handles same-origin cookies, JSON parsing, antiforgery headers, cancellation, and consistent error messages.
- Use server results for attendance times, work-day rules, pay-period state, and permissions. The client renders those values and does not reproduce business rules.
- Keep the initial screens responsive and accessible: semantic buttons, keyboard focus, labels, and visible loading/error feedback.
- Keep compensation display limited to the employee and authorized Admin/HR/Payroll users. Do not add payroll calculation, automatic email, or leave deductions without approved rules.

## Verification status

The frontend production build and backend unit-test suite have been run for the current local changes. Full Playwright browser scenarios are a deferred follow-up and have not been run.
