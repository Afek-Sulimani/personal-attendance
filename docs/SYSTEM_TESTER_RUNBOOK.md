# System tester runbook

This runbook is for a tester who needs to start the application, run its automated checks, and perform a short manual acceptance pass. It assumes a local macOS/Linux shell and a disposable SQL Server database.

## 1. Prerequisites

- .NET SDK 10 (`dotnet --version`). If the SDK is installed under the home directory, use `~/.dotnet/dotnet` instead of `dotnet`.
- Node.js 20.19+ or 22.12+ and npm.
- Docker Desktop/Engine with Compose v2, or an existing local SQL Server.
- Internet access for the API's configured external time providers. The end-to-end tests make real attendance requests against the synchronized clock.
- Chromium installed for Playwright. From `src/frontend/personal-web`, run `npx playwright install chromium` once if it is not already installed.

## 2. Prepare an isolated test database

Do not point Playwright at production or at a database containing records you need. E2E setup creates companies and users; the fixture does not delete those records after a run.

For Docker SQL Server, from the repository root:

```bash
if [ ! -f .env ]; then cp .env.example .env; fi
# Edit .env and set a unique MSSQL_SA_PASSWORD value.
docker compose up -d sqlserver
docker compose ps
```

Wait for `sqlserver` to report `healthy`. Then load the local SA password and create a dedicated database named `PersonalAttendanceE2E`:

```bash
set -a
source .env
set +a
SQL_CONTAINER="$(docker compose ps -q sqlserver)"
docker exec "$SQL_CONTAINER" /opt/mssql-tools18/bin/sqlcmd \
  -S localhost -U sa -P "$MSSQL_SA_PASSWORD" -C \
  -Q "IF DB_ID(N'PersonalAttendanceE2E') IS NULL CREATE DATABASE [PersonalAttendanceE2E];"
```

Use that database only for this run. The following connection string uses the local SQL Server administrator account so EF can create the schema and the API can use the isolated test database. Keep it local; never use it in a deployed environment or commit it.

## 3. Apply migrations

From the repository root, in the same shell where `.env` was sourced:

```bash
export ConnectionStrings__Attendance="Server=127.0.0.1,14333;Database=PersonalAttendanceE2E;User Id=sa;Password=${MSSQL_SA_PASSWORD};Encrypt=True;TrustServerCertificate=True;"
dotnet tool restore
dotnet ef database update \
  --project src/backend/Personal.Attendance.Infrastructure/Personal.Attendance.Infrastructure.csproj \
  --startup-project src/backend/Personal.Attendance.Api/Personal.Attendance.Api.csproj
```

If the local SQL Server is not Docker, first create an empty `PersonalAttendanceE2E` database and grant the test login schema-owner rights. Change the server/port and credentials in `ConnectionStrings__Attendance`. Apply migrations before starting the API; the application does not apply them automatically.

## 4. Start the API and web app

Keep each command running in its own terminal.

**Terminal 1 — API** (from repository root; source `.env` here too):

```bash
set -a
source .env
set +a
export ASPNETCORE_ENVIRONMENT=Development
export ConnectionStrings__Attendance="Server=127.0.0.1,14333;Database=PersonalAttendanceE2E;User Id=sa;Password=${MSSQL_SA_PASSWORD};Encrypt=True;TrustServerCertificate=True;"
export InvitationLinks__PublicBaseUrl=http://127.0.0.1:5175
dotnet run --project src/backend/Personal.Attendance.Api/Personal.Attendance.Api.csproj --urls http://127.0.0.1:5181
```

**Terminal 2 — Vite**:

```bash
cd src/frontend/personal-web
npm ci
printf 'ATTENDANCE_API_URL=http://127.0.0.1:5181\n' > .env.local
npm run dev -- --host 127.0.0.1 --port 5175
```

The ignored `.env.local` file points this test Vite server at the test API on port 5181. The regular developer app can continue using ports 5173/5081.

Check the API and app before testing:

```bash
curl --fail http://127.0.0.1:5181/api/health/live
```

Open `http://127.0.0.1:5175/login`. The automated tests use the same origin.

## 5. Run automated checks

**Backend unit tests** (from repository root):

```bash
dotnet test src/backend/Personal.Attendance.sln
```

**Frontend production build** (from `src/frontend/personal-web`):

```bash
npm run build
```

**End-to-end suite** (from `src/frontend/personal-web`):

```bash
npm run typecheck:e2e
E2E_BASE_URL=http://127.0.0.1:5175 npm run test:e2e
```

To run one scenario, pass its file path, for example:

```bash
E2E_BASE_URL=http://127.0.0.1:5175 npx playwright test e2e/scenarios/emp-04-correction-pending.spec.ts
```

The suite's global setup creates one uniquely named test company and Admin, Payroll, HR, Team Lead, and Employee accounts; scenarios then perform UI actions such as punches, correction requests, approval/rejection, invitations, and audited edits. All test accounts share the local-only password `FlowTest!2026` unless `E2E_TEST_PASSWORD` is set. Do not reuse it for a real account. Test users are not real mailboxes; the app does not send email.

The fixture currently leaves its database records behind. It may create more than one company during a full run. Use only `PersonalAttendanceE2E`, and reset that database before a clean rerun if required. The login rate limit may also reject rapid repeat runs with HTTP 429; wait for the rate-limit window to pass before retrying.

To create a small local demonstration company without running the full suite, run `E2E_BASE_URL=http://127.0.0.1:5175 npm run seed:local` from the frontend directory. It creates and activates exactly one Admin, Payroll, HR, Team Lead, and Employee in one company, configures the payroll cycle, verifies Employee navigation, and prints the company name/code, login URL, every user's display name, role and email, and the shared test password. Each run creates a new company with new email addresses. The default local-only password is `FlowTest!2026`; set `E2E_TEST_PASSWORD` before running if the tester needs a different test password. Do not reuse this password for real accounts. The account list is generated dynamically, so use the values printed by the latest seed run.

The generated users are:

| Display name | Role | Email pattern |
| --- | --- | --- |
| E2E Admin | Admin | `admin-<run-suffix>@example.test` |
| E2E Payroll | PayrollRepresentative | `payrollrepresentative-<run-suffix>@example.test` |
| E2E HR | HR | `hr-<run-suffix>@example.test` |
| E2E TeamLead | TeamLead | `teamlead-<run-suffix>@example.test` |
| E2E Employee | Employee | `employee-<run-suffix>@example.test` |

The suffix is unique for each run. These addresses are test identities, not real mailboxes.

## 6. Review results

- The terminal lists each scenario and its pass/fail result.
- `npm run test:e2e:report` opens the Playwright HTML report (`playwright-report/`).
- Screenshots, traces, and test artifacts are written under `test-results/`. Successful checkpoints are attached to the report; failures retain screenshots and traces. To inspect a trace, run `npx playwright show-trace <path-to-trace.zip>`.
- Both directories are ignored by Git. Do not attach `shared-workspace.json` to a public issue; it contains test session cookies.

## 7. Manual acceptance pass

Use a fresh company in the isolated database and check these flows in the browser:

1. Register a company with a unique test email (for example, `tester-<date>@example.test`). Confirm that the company ID has five digits and that the result provides a temporary activation link; no email is sent.
2. Open the temporary link, choose a password that meets the displayed rules, and sign in. Confirm the first company member has the Admin role.
3. In **Company settings**, configure a payroll-period start day of 16, the company time zone (`Asia/Jerusalem` by default), standard hours, and weekdays. Save, sign out, and sign in again if the existing session does not reflect updated role claims.
4. As Admin, invite Payroll, HR, Team Lead, and Employee accounts. Give each employee a direct manager and activate the accounts from their temporary links.
5. As an Employee, open **My attendance**. Check that live Clock In/Out is available only for the current company date and that an additional correction request replaces the previous pending request for that date.
6. Open **My monthly attendance**. With a payroll start day of 16, confirm the period begins on the 16th and ends on the 15th (for example, November 16–December 15 when today is November 21, 2026). Confirm that only scheduled workdays appear, a pending correction shows `Awaiting approval`, daily requested hours are visible, the total includes pending hours, and the pending subtotal is shown separately. Confirm there is no `NaN` in the totals.
7. As a Team Lead or eligible HR/Payroll/Admin reviewer, open **Review corrections**. Confirm pending requests are grouped into compact employee/day rows, approval applies the requested times, and rejection requires a reason. Check the red attention indicator only after the request's payroll period has ended.
8. As Admin, edit an existing shift in **Admin attendance** with a reason. Verify the saved before/after change appears in the audit history.
9. Open **My profile** and check that account/company identifiers and compensation details are visible to the employee, while unauthorized pay/admin pages remain denied.

Compare results with [the scenario catalog](user-flows/SCENARIOS.md) and [the automated-flow coverage notes](../src/frontend/personal-web/e2e/README.md). Record the scenario ID, account role, expected result, actual result, and screenshot/trace path for each failure.

## 8. Reset only the isolated E2E database

Stop the API first. This permanently deletes all test data in `PersonalAttendanceE2E`; verify the database name before running it. Do not use this against `PersonalAttendance` or any shared database.

```bash
set -a
source .env
set +a
SQL_CONTAINER="$(docker compose ps -q sqlserver)"
docker exec "$SQL_CONTAINER" /opt/mssql-tools18/bin/sqlcmd \
  -S localhost -U sa -P "$MSSQL_SA_PASSWORD" -C \
  -Q "ALTER DATABASE [PersonalAttendanceE2E] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [PersonalAttendanceE2E];"
```

Run the database-create and migration steps again before the next E2E run.
