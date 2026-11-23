# Current scope boundaries and known gaps

This file records work that is intentionally outside the implemented behavior or still needs product decisions. For current behavior, use [the architecture](docs/ARCHITECTURE.md), [the API inventory](docs/API_ENDPOINTS.md), and [the scenario catalog](docs/user-flows/SCENARIOS.md).

## Explicitly out of scope

- **Email delivery:** invitation links are shared manually. No email provider, scheduled email, or email reminder is configured.
- **Monthly report submission and approval:** monthly attendance is a read-only view. Employees and managers can view the same rows within their permissions; only individual attendance corrections require a manager decision.
- **Leave accounting:** a missed scheduled workday remains `No report`. The system does not deduct vacation, classify leave, or maintain a leave balance.
- **Payroll calculation:** `Global`/`Hourly` and the numeric hourly rate are employee profile settings only. The system does not calculate wages, taxes, overtime, deductions, or currency amounts.
- **Automatic cutoff jobs:** the configured payroll-period start day defines report windows. A review attention indicator appears only for pending correction requests whose payroll period has ended. No batch job is required to produce monthly reports.

## Implemented rules to preserve

- Each company has its own time zone; the default is `Asia/Jerusalem`. Each attendance punch stores its original time zone so later company setting changes do not rewrite historical display context.
- A payroll period starts on the configured day and ends the day before the next period starts. Report navigation selects the period by its start month.
- Monthly attendance tables contain scheduled workdays and dates with pending corrections; non-workdays are omitted. The total includes recorded shift time and complete pending correction time; pending time also appears as a separate subtotal.
- An overnight shift is assigned in full to its Clock In date. No time is split across calendar days or payroll periods.
- A worker may replace a pending correction for the same date; the newest request is the one reviewed.
- An Admin may edit an existing shift, including their own, with a reason and full audit history. Admin cannot create a shift where none exists.
- Activation links are one-time links that expire after 48 hours and are passed manually to the intended person.

## Remaining work before production use

- Replace in-process session storage and login throttling with shared, durable infrastructure before running multiple API instances.
- Require a verified system approval or another controlled onboarding process before enabling public company creation in production.
- Choose and document jurisdiction-specific labor rules before calculating payable hours, breaks, overtime, holidays, leave, or deductions.
- Expand Playwright coverage for multi-company isolation, expired/mismatched invitations, role combinations, historical time-zone changes, overnight shifts, password changes, payroll-boundary navigation, duplicate submissions, and mobile layouts across roles. The current scenarios are cataloged in `docs/user-flows/SCENARIOS.md` and the automated subset is in `src/frontend/personal-web/e2e/`.

## Verification limits

Backend tests use deterministic clocks and in-memory EF Core data; they do not prove SQL Server concurrency or database-trigger behavior. Playwright currently uses a local SQL Server and creates new test companies, so repeated runs should use a disposable database. See [TESTING.md](docs/TESTING.md).
