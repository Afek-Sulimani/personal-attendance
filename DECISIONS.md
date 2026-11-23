# Decisions, edge cases, and limitations

This document describes the implemented submission. It was consolidated retrospectively from the code, repository documentation, commits, and product decisions. It is not an original development transcript.

## Architecture

ASP.NET Core exposes HTTP endpoints; Application defines contracts; Domain owns shift invariants; Infrastructure implements EF Core SQL Server persistence and external time providers. React/TypeScript composes feature components and uses one HTTP client. This keeps authorization and payroll rules authoritative on the server. See [architecture](docs/ARCHITECTURE.md), [API catalog](docs/API_ENDPOINTS.md), and [database design](DATABASE_DESIGN.md).

## Authoritative time

Live Clock In/Out accepts no browser timestamp. TimeAPI.io with Timezone.io fallback supplies a validated sample. UTC is stored with provider provenance and the original time zone. Asia/Jerusalem is the default. A monotonic elapsed timer advances the external sample; server wall-clock and browser time do not originate attendance timestamps. Samples expire after 65 minutes; absence of a fresh sample rejects attendance with 503. Concurrent attendance failures share a five-second retry cooldown. Requests have bounded timeouts and validate zone, DST, offset, and duration. This limits provider traffic but is an estimate, not atomic-clock precision. See [clock design](docs/SYNCHRONIZED_CLOCK.md).

Company time-zone configurability is a product extension. For the exercise use Asia/Jerusalem. Manual corrections are explicitly separate from externally sourced live punches, require manager approval, and preserve source/audit information. Admin may edit an existing shift, including their own, with a mandatory reason and before/after audit; Admin cannot create an absent shift through that screen.

## Attendance and concurrency

One shift per employee, company, and work date is enforced by a unique SQL index; a second filtered unique index prevents multiple open shifts. Row versions detect concurrent edits. Unique violations return conflicts. Migration fails if historical duplicates exist; they must be reconciled explicitly rather than silently deleted.

A shift lasts at most 24 hours. All overnight hours belong to the Clock In date, including when the following date is a non-workday. Future punches/corrections are rejected. An existing personal monthly schedule is authoritative, including excluded days; defaults apply only when there is no personal schedule. Rejected requests do not prevent today's live Clock In. The newest pending correction replaces the previous pending request.

Monthly tables use the configured payroll period (for example, 16th through next month's 15th), omit non-workdays, display pending requested times, and include complete pending durations in the total with a separate pending subtotal. No report submission/approval workflow is required. Missing days stay No report; no leave balance is deducted. Correction attention appears after payroll cutoff and only for pending corrections.

## Accounts and authorization

A company's first representative is Admin. HR and Payroll may invite non-Admin roles; Admin may assign Admin. Employees have a direct manager. Upper-level representative requests may be reviewed by another eligible HR/Payroll member. Authorization rechecks company membership in SQL, rather than trusting hidden UI or cookie roles alone. Company identifiers have five digits; employee numbers increment per company, display padded to five digits, and accept unpadded search. Activation links expire after 48 hours and are shared manually; email delivery was intentionally omitted.

Cookie sessions use server-side memory, CSRF protection, and rate limits. Session cache and index mutations share a reentrant lock so removal/registration/eviction cannot detach live tickets from the revocation index. This simple lock serializes mutations across users, an acceptable throughput tradeoff for this single-process submission. Sessions disappear on restart; multi-instance deployment needs a shared ticket store and persisted Data Protection keys.

Compensation is Global or Hourly. Hourly amounts are numeric without a currency by explicit product decision. Employee profiles show memberships and personal information and support password changes.

## Original email workflow and final scope

The original product direction included two email flows: (1) invite an employee by email with an account activation link so they can choose their first password; (2) after the monthly payroll cutoff, email the employee's team lead to verify and approve that employee's monthly working hours. These were product ideas beyond the exercise's mandatory live-punch requirement.

During implementation, email delivery was explicitly removed from the current scope. The first flow now returns a single-use, 48-hour activation link to the authorized inviter, who shares it manually. Email is still the account identifier; the system does not send an invitation or independently verify mailbox ownership. This avoids requiring a configured mail provider for local evaluation.

The second flow was changed as well: employees and managers can continuously inspect payroll-period attendance tables, including pending requested hours. There is no Submit report button, monthly report approval endpoint, monthly approval state, scheduled end-of-month email, or job that performs monthly approval. Instead, the Review corrections navigation and employee grouping show red attention markers for pending correction requests after their payroll period ends. Managers approve or reject individual corrections; the marker is derived when reading the queue, not sent by a scheduled mail job. This follows the later product decision that attention is only for pending corrections.

A future email implementation would require a delivery provider, delivery/retry tracking, duplicate prevention, a scheduled payroll-cutoff job, secure links, and an explicit decision whether to restore whole-period approval. None of those are claimed as implemented. The product decision was to prioritize live punches, auditability, and local reproducibility over mail infrastructure.

## Loading feedback

A shared labeled spinner indicates initial loads and pending actions, with busy/disabled controls and reduced-motion support. Background calendar refresh preserves content. Errors stop the loading indicator. See [loading feedback](docs/LOADING_FEEDBACK.md).

## Verification and deferred work

Unit tests cover domain, time, persistence metadata, authorization and session behavior. Playwright covers role flows with screenshots/traces. [E2E documentation](src/frontend/personal-web/e2e/README.md) lists remaining automation gaps; passing the suite does not prove every catalog scenario. A local opt-in seed creates one activated account per role; production migrations never contain sample credentials.

Deferred: email delivery, leave balances, configurable shift duration, production hosting and distributed sessions, full E2E coverage, automated isolated database provisioning/cleanup, and deployment hardening. Tests leave records in a disposable database. Apple Silicon SQL Server uses unsupported amd64 emulation. External provider availability is required. See [tester runbook](docs/SYSTEM_TESTER_RUNBOOK.md) for reproducible setup and known test limits.

## AI work evidence

[CLAUDE.md](CLAUDE.md) imports [.claude/CLAUDE.md](.claude/CLAUDE.md); local skills describe testing and review. [Retrospective work plan](docs/plans/RETROSPECTIVE.md) maps the implementation and user corrections. These artifacts document guidance and outcomes, but do not establish that every change was produced by Claude Code. No missing historical transcript has been fabricated.


## Personal edition

The personal edition supersedes assignment-specific defaults; see [Personal edition decisions](docs/PERSONAL_EDITION.md). Israel is the new default, CSV exports use the visible payroll period, and Google login preserves existing company authorization.
