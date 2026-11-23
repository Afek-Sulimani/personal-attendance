# Personal Attendance

A personal attendance management platform. Start with [Personal edition and Google setup](docs/PERSONAL_EDITION.md).


A small ASP.NET Core and React project for recording attendance against a synchronized company clock. `Asia/Jerusalem` is the default time zone; each company can configure its own.

**Current implementation:** an API clock that refreshes external time in the background, SQL Server persistence, company-scoped login/session endpoints, attendance APIs, employee invitations, company registration, payroll/time-zone settings, read-only monthly attendance tables, company-specific compensation settings, and React pages for sign-in, account activation, company registration, attendance, employee profiles, employee pay details, reports, invitations, workday setup, correction review, and Admin attendance changes. The first company representative receives the Admin role. Activation links are copied and passed manually; the application does not send email.

## Technology

- ASP.NET Core 10 Web API.
- React + TypeScript with Vite, rendered as a client-side SPA.
- Microsoft SQL Server through EF Core 10. Migrations create identity, company, attendance, company settings, monthly work schedule, correction, invitation, and Admin audit tables.

For first-time setup, follow the [system tester runbook](docs/SYSTEM_TESTER_RUNBOOK.md), including database creation and migrations before starting the API. Read [DECISIONS.md](DECISIONS.md) for rationale, edge cases, and known limitations; the [retrospective work plan](docs/plans/RETROSPECTIVE.md) records the implementation stages.

## Run locally

The .NET 10 SDK is installed under `$HOME/.dotnet`. If the terminal cannot find it, run:

```bash
export PATH="$HOME/.dotnet:$PATH"
```

Start the API in one terminal from the repository root:

```bash
dotnet run --project src/backend/Personal.Attendance.Api/Personal.Attendance.Api.csproj --urls http://localhost:5081
```

Start the web page in another terminal:

```bash
cd src/frontend/personal-web
npm ci
npm run dev
```

Open `http://127.0.0.1:5173/login` to sign in, or `/register-company` to create a company and its first company representative invitation. Open `/dev/time-check` for the temporary provider-check page; each button requests one provider independently through the API.

The Vite development server listens on the local network so the application can also be opened from a phone connected to the same Wi-Fi. Use the computer's LAN address, for example `http://<computer-lan-ip>:5173/login`. On macOS, `ipconfig getifaddr en0` shows the current Wi-Fi address. To make invitation links open on that phone, set the local `InvitationLinks:PublicBaseUrl` user secret to `http://<computer-lan-ip>:5173`; update it if the computer's LAN address changes. API calls still pass through Vite to the local API.

The API also exposes the read-only clock endpoint at `http://localhost:5081/api/clock/current`. It returns HTTP 503 when it has no fresh external sample. It does not save attendance.

The attendance API exposes `GET /api/attendance/current`, `GET /api/attendance/calendar`, `POST /api/attendance/clock-in`, `POST /api/attendance/clock-out`, and correction request/review routes under `/api/attendance/corrections`. Live punches do not accept a timestamp or employee/company identifier from the browser. They use the company's configured time zone; new companies default to `Asia/Jerusalem`. Corrections store employee-proposed local times in a review request and apply them to a shift only after the assigned manager approves; if the employee submits another request for the same date, the latest request replaces the pending one. The submission itself is timestamped from the external clock. Shifts are limited to 24 hours between Clock In and Clock Out. Admin or Payroll can configure company settings, and Payroll manages monthly employee workdays through `/api/payroll/employees`. Admin can edit an existing shift with a reason and a before/after audit record. See [docs/ATTENDANCE_PUNCH_DESIGN.md](docs/ATTENDANCE_PUNCH_DESIGN.md) for flows and constraints.

Monthly attendance views use the company's configured payroll-period start day. For example, a cycle starting on the 16th runs October 16–November 15; the table shows scheduled workdays in that range, omits non-workdays, and assigns an overnight shift to its Clock In date. The total includes completed shifts and complete correction requests awaiting approval; pending hours are also shown separately. See [docs/API_ENDPOINTS.md](docs/API_ENDPOINTS.md) for the endpoint contract and query semantics.

SQL Server can run locally through `docker compose up -d` after copying `.env.example` to `.env`, or you can connect to an existing SQL Server instance. The database context is registered only when `ConnectionStrings:Attendance` is configured locally. Migration, Docker, Rider, and existing-server setup steps are in [docs/DATABASE_SETUP.md](docs/DATABASE_SETUP.md). The official SQL Server Linux container is x86-64; the Apple Silicon development setup uses amd64 emulation and is not officially supported by Microsoft. Authentication endpoints return HTTP 503 without a reachable SQL Server connection and require an existing confirmed account with an active employee membership; the invitation API creates or activates those accounts.

The login page accepts the exact company name or company ID, plus email and password. If company names are duplicated, sign in with the company ID. The API stores its 12-hour session ticket in process memory. See [docs/AUTHENTICATION.md](docs/AUTHENTICATION.md) for the flow, security boundaries, and limitations.

Employee invitations are created through the backend API and stored in SQL Server. Their links expire after 48 hours, can be used once, and are passed manually by the company representative or HR/Payroll contact. No email provider is configured. After activating the first Admin account, open `/payroll/company-settings` to configure the company's payroll cycle, time zone, standard work hours, and default weekdays. Open `/admin/people` to invite additional members and `/admin/attendance` to inspect or edit existing attendance shifts. See [docs/INVITATION_LINKS.md](docs/INVITATION_LINKS.md) for the API contract and [docs/COMPANY_REGISTRATION.md](docs/COMPANY_REGISTRATION.md) for the registration flow.

If ports 5081 or 5173 are already in use, run the API on another port and point Vite at it:

```bash
dotnet run --project src/backend/Personal.Attendance.Api/Personal.Attendance.Api.csproj --urls http://127.0.0.1:5181
```

In the frontend terminal, set the proxy target and choose a free Vite port:

```bash
ATTENDANCE_API_URL=http://127.0.0.1:5181 npm run dev -- --port 5174
```

The Vite proxy then forwards browser API calls to `http://127.0.0.1:5181`.

The background service requests providers in configured order at startup and every hour. It prefers TimeAPI.io, then Timezone.io. The sample expires after 65 minutes; if refresh fails, a request may use the last sample only while it remains fresh. A diagnostic request without a fresh sample respects the five-minute retry cooldown. An attendance punch without a fresh sample attempts provider fallback, sharing a failed attempt with concurrent requests and suppressing additional attempts for five seconds before returning HTTP 503.

The estimate advances from an external UTC sample using monotonic elapsed time. Half of the measured request duration is added as a midpoint estimate; it is not a guarantee of atomic-clock precision. Live attendance events use this synchronized clock and are stored in UTC with provider provenance, at minute precision. Approved corrections retain a separate manual-correction source and approval record.

## Project guidance

- [docs/LOADING_FEEDBACK.md](docs/LOADING_FEEDBACK.md) documents shared loading indicators and accessibility behavior.

- [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md) describes the current application boundaries.
- [docs/AUTHENTICATION.md](docs/AUTHENTICATION.md) describes company-scoped login, CSRF protection, and in-memory sessions.
- [docs/INVITATION_LINKS.md](docs/INVITATION_LINKS.md) documents the invitation API, activation-link security, and manual delivery.
- [docs/COMPANY_REGISTRATION.md](docs/COMPANY_REGISTRATION.md) documents company registration and first-account activation.
- [docs/ATTENDANCE_PUNCH_DESIGN.md](docs/ATTENDANCE_PUNCH_DESIGN.md) describes the backend attendance endpoints, validation flow, and limits.
- [DATABASE_DESIGN.md](DATABASE_DESIGN.md) shows the current core SQL Server model.
- [docs/DATABASE_SETUP.md](docs/DATABASE_SETUP.md) explains local connection setup and migration commands.
- [docs/TIME_PROVIDER_CHECK.md](docs/TIME_PROVIDER_CHECK.md) explains the temporary external-time request flow.
- [docs/FILE_GUIDE.md](docs/FILE_GUIDE.md) explains the main repository file groups and current scope.
- [docs/COMMIT_GUIDE.md](docs/COMMIT_GUIDE.md) defines the commit message format and proposed commit breakdown.
- The project code-review skill is in `.claude/skills/code-review/SKILL.md`.
- [docs/SYNCHRONIZED_CLOCK.md](docs/SYNCHRONIZED_CLOCK.md) explains the clock design, freshness, fallback, and limits.
- [docs/TESTING.md](docs/TESTING.md) contains the test commands and conventions.
- [docs/SYSTEM_TESTER_RUNBOOK.md](docs/SYSTEM_TESTER_RUNBOOK.md) gives a system tester the full setup, migration, run, manual-acceptance, result-review, and isolated-database-reset steps.
- [docs/API_ENDPOINTS.md](docs/API_ENDPOINTS.md) lists every HTTP endpoint and its purpose.
- [src/frontend/personal-web/e2e/README.md](src/frontend/personal-web/e2e/README.md) documents the Playwright role flows and their remaining coverage gaps.
- The project unit-test skill is in `.claude/skills/dotnet-unit-tests/SKILL.md`.
