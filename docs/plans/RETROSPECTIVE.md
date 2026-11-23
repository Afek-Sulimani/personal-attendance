# Retrospective implementation plan

Reconstructed on 2026-11-21 from the repository and product discussion. This is a retrospective, not a recovered original Claude Code plan.

1. Establish ASP.NET Core, React, SQL Server, EF migrations, and architectural boundaries.
2. Build externally sourced Israel time with validation, fallback, freshness, monotonic advancement, and fail-closed attendance.
3. Add company registration, activation, authentication, role permissions, and employee invitations.
4. Configure payroll cycle, weekly defaults, monthly overrides, company zones, and historical zone preservation.
5. Implement live attendance, overnight limits, correction replacement, manager decisions, and audited Admin edits.
6. Build responsive navigation, calendar, compact approval tables, payroll-period reports, pending totals, compensation and profiles.
7. Add role-specific Playwright page objects/scenarios, unit tests, screenshots/traces, seed and reviewer setup instructions.
8. Harden session revocation races, shift uniqueness, personal-calendar exclusions, failed time refresh sharing, and UI refresh behavior; consolidate decisions and verify the submission.

User corrections that shaped the result: first representative is Admin rather than HR/Payroll; invitations use manually shared links instead of email; reports are read-only without Submit report; pending times must appear before approval; periods follow payroll cutoff; overnight hours belong to entry date; input borders and mobile navigation must remain clear. These were requirements clarified during implementation, not assumptions about original tool output.

Known AI/development corrections: SQL Server Docker environment variables do not provision databases or application logins, so setup instructions now create the database explicitly. ConcurrentDictionary did not make the multi-step session index lifecycle atomic, so mutations are now synchronized. An existing personal schedule must override defaults even for excluded dates. Future work and coverage gaps are recorded in DECISIONS.md and the E2E README.

Original ideas that changed: invitation links were initially intended for email delivery; monthly payroll-cutoff emails to a team lead were intended to initiate validation/approval of employee hours. Delivery was removed, and whole-month approval was replaced by read-only payroll-period reporting plus individual correction approval and in-app post-cutoff attention. See DECISIONS.md for rationale and deferred infrastructure.
