# Backend Tests

## Run the backend suite

From the repository root:

```bash
dotnet test src/backend/Personal.Attendance.sln
```

If `dotnet` is not on `PATH` and the SDK is installed under the user's home directory, run:

```bash
~/.dotnet/dotnet test src/backend/Personal.Attendance.sln
```

## Coverage

- Synchronized clock snapshot advancement, expiration, Israel winter/summer offsets, and invalid sample handling.
- Provider ordering, fallback behavior, cancellation, and request throttling using fake providers.
- Domain rules for company-code normalization and UTC minute-precision work-shift events.
- EF Core SQL Server model metadata for `ATTENDANCE_*` names, unique email, company-scoped relationships, one open shift, and shift row-version concurrency.
- Company-scoped login through ASP.NET Core Identity using EF Core InMemory test data: valid credentials, wrong password/company, and unconfirmed email.
- In-memory authentication-ticket store: opaque ticket key, ticket renewal, and logout removal.
- Authentication service registration: server-side ticket storage, 12-hour non-sliding expiration, production cookie flags, and antiforgery header configuration.
- Attendance service behavior: minute truncation, provider provenance, clock-in/out transitions, active membership enforcement, current open-shift lookup, and fail-closed time-source errors.
- Employee invitation behavior: 48-hour expiration, secure token hashing, single-use acceptance, reissue revocation, HR/Payroll role boundaries, new-account activation, and joining a second company with an existing account.
- Employee profile behavior: active multi-company profile details, Admin/HR/Payroll compensation access, company isolation, password policy responses, and Global/Hourly data rules.
- Session behavior: changing a password removes all in-memory tickets belonging to that user and preserves other users' sessions.
- Monthly attendance: payroll-period boundaries, current and selected periods, overnight shifts assigned to Clock In date, scheduled-workday rows, manager access, and combined recorded-plus-pending totals with a separate pending subtotal.
- Correction attention policy: only a pending correction whose payroll period has ended requires attention; the visual badge and grouped approval-table row are covered by Playwright.

Tests use fixed timestamps and fake time readings; they do not call external time providers or connect to SQL Server. Database mapping tests inspect the EF model only. EF Core InMemory tests do not prove SQL Server unique-index or row-version behavior.

## Browser flows

For the complete tester procedure—including an isolated SQL Server database, exact API/Vite ports, Playwright setup, manual acceptance steps, result artifacts, and database reset—follow the [System Tester Runbook](SYSTEM_TESTER_RUNBOOK.md).

The Playwright suite creates companies and accounts, submits punches and correction requests, and updates test records. Its fixtures do not delete those records after a run. Use a disposable database; do not run the suite against production or a database containing records you need. See [e2e/README.md](../src/frontend/personal-web/e2e/README.md) for scenario coverage and known automation gaps.

## Claude Code guidance

Before changing or adding backend tests, read `.claude/skills/dotnet-unit-tests/SKILL.md`. Use deterministic test data and report only commands that actually completed.
