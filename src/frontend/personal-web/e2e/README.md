# Playwright user flows

For a safe, reproducible setup, follow the repository's [System Tester Runbook](../../../../docs/SYSTEM_TESTER_RUNBOOK.md). It creates an isolated `PersonalAttendanceE2E` database, starts the API on port 5181 and Vite on port 5175, and includes the migration and cleanup commands.

Once those services are running, from this directory run:

```sh
npm run typecheck:e2e
E2E_BASE_URL=http://127.0.0.1:5175 npm run test:e2e
```

`playwright.config.ts` rejects remote targets. The fixture creates unique companies and accounts and does not remove them afterward, so use only a disposable test database. All generated accounts use the same password from `TEST_PASSWORD` (`FlowTest!2026`), or `E2E_TEST_PASSWORD` when supplied. Never use that password for a real account.

To populate that local database with one activated member of each role, run `E2E_BASE_URL=http://127.0.0.1:5175 npm run seed:local`. The command prints the company name/code, login URL, all five display names, roles, generated emails, and the shared password. The roles are Admin, PayrollRepresentative, HR, TeamLead, and Employee; display names are E2E Admin, E2E Payroll, E2E HR, E2E TeamLead, and E2E Employee. Addresses use a unique run suffix and the `example.test` domain, so copy the exact addresses from the latest command output. Set `E2E_TEST_PASSWORD` first if the default `FlowTest!2026` is unsuitable. This is opt-in test data; it is never inserted by production migrations, and the password must not be used for real accounts.

Each scenario has its own `*.spec.ts` file. Page objects inherit `BasePage` for opening a route, checking its heading, and attaching screenshots. Authenticated pages inherit `ApplicationPage` for navigation, roles, and sign out. `TestWorkspace` performs API fixture setup; `globalSetup` creates one company with all five roles, while scenarios exercise the user interface. Browser sessions are separate by role. Screenshots on failures and traces are written under the ignored `test-results/` directory; the explicit `capture` calls attach successful checkpoints to the HTML report.

## Coverage

The current suite covers onboarding and Admin activation; Admin navigation, invitation and audited shift edit; Payroll settings, monthly workday override and hourly compensation; HR invitation permissions; Team lead navigation, approval, rejection, empty queue, and post-cutoff correction attention; Employee navigation, live punches, calendar restrictions, pending correction rows with combined and separate totals, replacement of pending corrections and forbidden Admin/pay management access; invalid login; and the mobile menu. Some files cover closely related assertions from the same end-to-end flow.

The scenario catalog in `docs/user-flows/SCENARIOS.md` contains additional flows that are **not automated yet**: full role combinations and manager search, Global compensation, historical time-zone changes, password changes, overnight shifts, two-company isolation, expired and mismatched invitation links, rapid duplicate submissions, payroll boundary months, and the full mobile layout on every role. Monthly views are tested as read-only attendance tables; there is no monthly report approval flow. Several scenario files cover only part of a catalog row: for example, `PAY-05` checks Hourly but not invalid rates. `EMP-10` and `EMP-11` together check direct access to the Admin and pay routes.

## Attendance hardening regression coverage

EMP-12 checks that a rejected correction permits today's live Clock In. EMP-13 verifies focus and one-minute server refresh. EMP-14 simulates a server date rollover and verifies yesterday no longer offers live punches. These three UI contract tests intercept calendar/current responses; the existing live punch, correction, approval and payroll scenarios still use the real local API and SQL Server. The UI tests do not claim to test an actual overnight wait or external provider outage.

Backend tests additionally cover personal-calendar exclusions for live punches and correction requests, personal-day inclusion/calendar flags, concurrent session registration/removal, unique work-date model metadata, and twenty concurrent failed time-refresh requests plus cooldown/retry.

## Known product gaps observed by the suite

- `LEAD-03` now verifies that a Team lead with direct reports sees an empty queue without HTTP 403. This also lets the queue refresh cleanly after the last request is reviewed.
- Direct Employee access to Admin attendance is denied by the API and now shows a specific access-denied message in the page.
- `EMP-04` confirms that complete pending correction duration is included in the displayed total and is also shown as its own awaiting-approval subtotal.

The fixture uses the existing local application and database. It does not yet provision an isolated database or delete its test companies after each run. The login endpoint limits attempts per minute, so repeatedly launching the suite in quick succession can return HTTP 429 during fixture setup. Keep the production rate limit; use a fresh disposable database and allow the window to clear between local reruns.
