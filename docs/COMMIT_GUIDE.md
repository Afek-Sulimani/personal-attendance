# Commit Guide

## Subject format

Use a Conventional Commit subject with one logical change per commit:

```text
<type>(<scope>): <short imperative summary>
```

Use the body to explain why the change exists, what changed, which files matter, and which checks actually ran. Do not include credentials, personal test data, or claims about checks that were not performed.

```text
Purpose:
<Why this change is needed.>

Changes:
- <Concrete change>

Files:
- `path/to/file` — <Plain-language explanation>

Checks:
- <Commands or manual checks actually performed>
```

## Main branch protection requirement

- Protect `main` from force pushes and deletion.
- Merge changes into `main` through pull requests; do not push directly to `main`.
- Once CI checks are configured, require the applicable checks to pass before merging. Do not require checks that are not configured or do not report a result.

## PR #3 — core attendance persistence

PR #3 adds the initial Identity, company membership, employee profile, company role, and work-shift schema. It does not add HTTP login or punch workflows.

## PR #4 — authentication and in-memory sessions

The proposed commits are:

1. `feat(auth): verify employee credentials within a company`
   - Add the application authentication contract and Identity-backed company/membership lookup.
   - Require confirmed email, valid password, active company, active membership, and employee profile.
   - Add deterministic tests for valid credentials, wrong company/password, and unconfirmed email.
2. `feat(api): add secure in-memory login sessions`
   - Add antiforgery, login, current-session, and logout endpoints.
   - Store cookie authentication tickets in `IMemoryCache`, with a 12-hour configurable absolute expiry.
   - Add secure cookie flags and bounded login requests; add ticket-store tests.
3. `docs: describe authentication and session flow`
   - Document the login sequence, endpoint contracts, cookie/CSRF rules, session limits, and future invitation boundary.

PR #4 intentionally excludes invitation creation/acceptance, email sending, user-management UI, punch endpoints, and sample seed data. PR #5 promoted its changes to `main` after PR #4 had been merged into the intermediate branch.

## Attendance workspace feature

The attendance workspace combines server-validated punches, a monthly workday calendar, Payroll schedule setup, and approved correction requests. A suggested local commit split is:

1. `feat(attendance): add monthly schedules and calendar API`
   - Add employee monthly schedules, configured work dates, calendar status projection, and live punch validation against the schedule.
2. `feat(attendance): add manager-reviewed correction requests`
   - Add the correction request schema and migration, assigned approver routing, representative fallback, transactional approval, and required rejection reason.
3. `feat(web): add attendance and payroll workspace`
   - Add the responsive role-aware sidebar, employee calendar and punch controls, Payroll workday calendar, and review queue.
4. `docs(attendance): document workspace and time-source rules`
   - Update the API, database, setup, and project-instruction documentation.

## Employee invitation feature

The invitation API returns a 48-hour, single-use activation URL for HR or Payroll to deliver manually. The URL contains the token in a fragment; SQL Server stores only its hash. The application does not send email. The React activation screen is implemented.

Keep the implementation reviewable in these commits:

1. `feat(invitation): add secure employee activation links`
   - Add the single invitation table, EF Core migration, 48-hour token lifecycle, role checks, create/validate/accept endpoints, and account/membership activation.
2. `test(invitation): cover token lifecycle and access rules`
   - Cover expiry, one-time use, link replacement, role permissions, new accounts, and existing multi-company accounts using fixed time.
3. `docs(invitation): document activation API and manual delivery`
   - Document route contracts, request/response examples, 48-hour expiry, local URL limits, and the UI/email boundaries.

## Company registration and account setup pages

The company registration flow creates a company and its first Admin invitation in one database transaction. The returned activation URL is copied and passed manually; no email service is involved.

1. `feat(company): create company and initial representative invitation`
   - Add five-digit company code generation, transactional persistence, the registration endpoint, and its nullable invitation creator migration.
2. `feat(web): add company registration and account setup pages`
   - Add the login, activation, and company-registration routes, shared API client, responsive authentication layout, and link-copy behavior.
3. `docs: describe company registration and account activation`
   - Document page/API behavior, manual activation-link delivery, and local verification results.

## PR — Company Admin setup and time-zone aware attendance

Use the following focused commits for the company settings, per-company time-zone, Admin attendance edit, and role-aware page changes:

1. `feat(company): configure Admin role and company payroll settings`
   - Give the first company representative only the Admin role and enforce invitation assignment rules.
   - Add company payroll/time-zone settings, default weekdays, their API/service, and the EF Core migration.
   - Route time synchronization and live attendance through the company-configured time zone while retaining each punch's saved zone.
   - Include backend test fixture updates required by the new time-zone clock interface.
2. `feat(attendance): add audited Admin shift corrections`
   - Add an Admin-only API for listing monthly existing shifts and editing their punch values.
   - Require a reason and persist each before/after snapshot with actor and external audit timestamp in one transaction.
   - Add the Admin attendance page and show the full actor, date, source, and provider/sample history.
3. `feat(web): add company setup and employee administration pages`
   - Add the company settings form, employee invitation page, role-filtered navigation, and route/API wiring.
4. `docs: document company settings and Admin attendance rules`
   - Update project instructions, architecture, database design, attendance flow, file guide, and local run guidance.
