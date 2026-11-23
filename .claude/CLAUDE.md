# Personal Attendance Project Instructions

## Product and stack

- Product name: Personal Attendance.
- Backend: ASP.NET Core modular monolith split into `Api`, `Application`, `Domain`, and `Infrastructure` projects.
- Frontend: responsive React + TypeScript single-page app built with Vite.
- Database: Microsoft SQL Server through EF Core 10. Use `ATTENDANCE_*` table names and GUID product IDs.
- Keep repository files and project documentation in English.
- Keep local design drafts, conversation logs, personal addresses, credentials, and IDE metadata out of Git.

## Roles and company onboarding

- Company roles are `Admin`, `Employee`, `TeamLead`, `HR`, and `PayrollRepresentative`. A membership can hold multiple roles; one global user can join multiple companies.
- Company registration creates the company and an invitation for its first representative. That representative receives `Admin` only.
- An active company Admin can invite members and assign company roles. HR and Payroll can invite members and assign any non-Admin roles. Always enforce these rules in Infrastructure against current SQL Server memberships, not only cookie role claims.
- Every invited employee must have a direct manager selected. The invitation creator is not automatically the manager or correction approver.
- The company has one optional payroll-settings row. Admin or Payroll can configure payroll-period start day, company time zone, standard work start/end times, and the default workweek.
- `Asia/Jerusalem` is the default company time zone. A company may configure another supported system time-zone ID.
- A monthly employee schedule overrides the company's default workweek for that calendar month. Do not infer a payroll period, standard hours, or workdays before they are explicitly configured.
- Public company registration is supported for local use. Review onboarding policy before deploying publicly.

## Identity, authorization, and sessions

- Display name and a five-digit company-scoped employee number belong to `EmployeeProfile`. The number is allocated when an invitation is accepted; the first active representative is `00001`. Search inputs may accept `1` for `00001`. `UserName` is technical; login uses company code, email, and password.
- The server derives user, company, membership, employee profile, and roles from authenticated claims, then rechecks active membership and required roles in SQL Server.
- Login requires a confirmed email, valid password, active company, active company membership, and employee profile. Return a generic login error for invalid credentials or company membership.
- Use ASP.NET Core cookie authentication with a server-side `ITicketStore` backed by `IMemoryCache` for the single-instance local setup. Session lifetime is 12 hours; sliding renewal is disabled.
- Login and logout require antiforgery tokens. Production cookies must be `Secure`, `HttpOnly`, `SameSite=Lax`, `Path=/`, and use the `__Host-` prefix.
- Restarting the API clears sessions. Multiple API instances require shared ticket storage, shared Data Protection keys, suitable load balancing, and shared login rate limiting.
- Login rate limit is 15 attempts per minute per source IP during development. Treat it as a development safeguard, not a production policy.
- Passwords require at least 6 characters, uppercase and lowercase letters, a digit, and a non-alphanumeric symbol. Keep activation and password-change guidance aligned with the server's ASP.NET Core Identity policy.
- The signed-in employee may view their email, username, active company memberships, company codes, employee numbers, roles, and company-specific compensation settings. The employee may change their password after verifying the current password.
- A successful password change revokes all of that user's sessions in the current in-memory ticket store; the user signs in again. This behavior is local to the single API instance.

## Employee profiles and compensation

- Compensation belongs to an employee's company-specific `EmployeeProfile`, not to the global user account. A user who belongs to several companies can have different compensation types in each company.
- Admin, HR, and Payroll may configure an employee as `Global` or `Hourly`. An hourly profile requires a positive numeric rate with at most two decimal places; a global profile has no hourly rate. Do not attach a currency unless a product decision adds one.
- The employee can view their own compensation type and hourly rate. Do not expose another company's profile through a company-scoped management endpoint.
- Keep compensation configuration separate from payroll calculation. Do not calculate gross pay, tax, deductions, or leave balances.

## Invitations and email

- Employee invitation links expire after 48 hours, are single-use, and are stored only as token hashes. Never log or persist raw tokens.
- The backend returns an activation URL for the inviter to pass to the employee. Do not add automatic email delivery or an email provider unless the product decision changes.
- Build activation links from `InvitationLinks:PublicBaseUrl`, never from an untrusted request `Host` header.

## Attendance time integrity

- Every live Clock In and Clock Out time must come from a valid external time API sample for the company's configured time zone. New companies default to `Asia/Jerusalem`.
- A synchronized in-process clock may advance from an external sample with monotonic elapsed time; refresh providers hourly, with configured fallback order. A live punch uses a fresh sample or immediately attempts external fallback. If no valid sample is available, reject the punch.
- Never use browser time, server wall-clock time, or database time as the recorded time of a live punch.
- Store event instants in UTC, truncate down to minute precision, and retain the provider and provider-sample time for live events.
- Store the time-zone ID on each punch and preserve it for historical display. Clock In determines the shift's `WorkDate`; Clock Out retains that date even if it occurs the next local day or the company's time-zone setting later changes.
- Employee corrections are requests for manager review, not live punches. Store their selected time zone, proposed UTC time, and external submission-time provenance separately. Never claim an external provider supplied a proposed historical time.
- A shift is limited to 24 hours. Treat the limit as fixed until HR/Payroll configuration is explicitly implemented.

## Admin attendance changes

- Company Admin may correct any existing shift, including their own, without a second approval. Do not allow this feature to create a shift where none exists.
- Require a non-empty reason for every Admin edit. Save the edited shift and an audit row in one SQL transaction.
- Audit the actor membership, reason, before/after dates and punch values, punch zones/sources/provider provenance, and the external sample used for the change timestamp.
- Admin edits preserve the stored time zone of each existing punch. Reject ambiguous or nonexistent local times around daylight-saving transitions and reject future times.

## Work schedules and payroll scope

- Payroll-period start day, standard work hours, company time zone, and company default weekdays are company settings. Each setting is editable by Admin or Payroll.
- Payroll can choose an employee's working weekdays for a calendar month; the UI expands them into dates and previews the month. A saved monthly schedule overrides company default weekdays.
- Employee-specific workday overrides remain calendar-month settings, while attendance reports use the company's payroll period. A period starts on the configured day and ends the day before the next period starts; `year`/`month` navigation selects the month containing the period start date.
- Monthly attendance tables omit non-workdays and show scheduled workdays in the selected payroll period. A full shift, including an overnight shift, belongs to its Clock In date; do not split shift duration at midnight or a payroll-period boundary.
- The total attendance duration includes recorded shifts and complete pending correction durations. Show pending correction hours as a separate subtotal. Display a pending correction as `Awaiting approval` in the employee's report until a reviewer decides it.
- Validate workdays and Clock In transitions on the server even when React disables an action.
- This feature does not calculate gross payroll, taxes, deductions, or leave balances. A missed scheduled workday remains `No report`; do not deduct vacation or classify leave.
- Monthly attendance is a read-only view, not a submitted or manager-approved report. Reviewers approve or reject individual attendance correction requests. The post-cutoff attention indicator applies only to pending corrections whose payroll period has ended.
- Team leads may view their direct reports; Admin, HR, and Payroll may view company employees. Keep the review queue compact and group correction rows by employee.
- Do not add monthly report submission/approval, automatic email, or leave deduction unless the user changes these product decisions.

## Frontend rules

- Keep page components focused on composition. Put reusable visuals in `components` and feature API contracts/calls near their feature.
- Route HTTP calls through the shared API client; use its antiforgery behavior for state-changing requests.
- Keep business rules, authorization, payroll outcomes, and authoritative time decisions on the backend.
- Use semantic HTML, explicit labels, keyboard-visible focus, accessible status/error messages, and clear loading/success/failure states.
- Keep desktop and mobile layouts usable. Avoid horizontal scrolling except inside a wide data table where it preserves the table layout; avoid oversized components, duplicated CSS, or unapproved product flows.
- Preserve the existing Personal visual style unless the requested feature requires a change.

## SQL Server and migrations

### First run for a project maintainer

- Start with `docs/SYSTEM_TESTER_RUNBOOK.md`; it gives the exact first-run order, commands, ports, migration, seed, and test procedure. `docs/DATABASE_SETUP.md` explains ordinary local SQL Server and Rider connections. The root `CLAUDE.md` imports this file.
- From the repository root, create an ignored `.env` from `.env.example` and set unique local passwords. Run `docker compose up -d sqlserver` and wait for `docker compose ps` to show `healthy`. Docker starts SQL Server on `127.0.0.1:14333`; it does **not** create a database, login, or apply EF migrations by itself.
- For the maintainer's disposable database, follow the runbook's `sqlcmd` step to create `PersonalAttendanceE2E`. Set `ConnectionStrings__Attendance` to that database on `127.0.0.1,14333`, using the local `sa` password from `.env`, then run `dotnet tool restore` and `dotnet ef database update` with the project/startup-project paths shown there. Never use a database containing records that must be retained for E2E.
- Start the API and Vite in separate terminals with the environment variables and ports in the runbook. For a visible sample company with Admin, Payroll, HR, Team Lead, and Employee, run `npm run seed:local` from `src/frontend/personal-web` against the test Vite URL. The command prints the company code and login emails; the test password is described in the runbook. Do not put real credentials or seed users in migrations.

- Keep EF Core mappings explicit under `Infrastructure/Persistence/Configurations`.
- Keep migrations under `Infrastructure/Persistence/Migrations`; review the migration and model snapshot together.
- Do not auto-apply migrations at production startup. Document commands needed by local reviewers.
- Keep connection strings and credentials in local user secrets or ignored environment files.
- Do not add seed data unless specifically requested.
- Keep demonstration accounts behind an explicit local seed command; do not insert their credentials in production migrations.

## Tests and reviews

- When adding or changing .NET tests, read `.claude/skills/dotnet-unit-tests/SKILL.md`. Use fixed times and fakes; never call live time providers, SQL Server, or email providers from unit tests.
- When asked for a code review, follow `.claude/skills/code-review/SKILL.md`.
- When changing API behavior, keep controller contracts, frontend types, and relevant documentation aligned.
- Follow the main-branch protection policy in `docs/COMMIT_GUIDE.md`; merge through pull requests and do not push directly to `main`.
