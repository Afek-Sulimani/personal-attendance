# Personal Attendance edition

This personal attendance platform uses
`Personal.*` projects, `ATTENDANCE_*` tables and a separate `PersonalAttendanceE2E`
database for testing. Use a dedicated database for each environment.

## Features

- Default company zone: `Asia/Jerusalem` (Israel). Admin/Payroll can search the
  server's time-zone catalogue by region/city in Company settings. IANA identifiers
  represent zones, not every physical city; towns sharing a clock share an identifier.
  Each punch retains its original zone. Existing historical records are never converted.
- CSV download on personal and team payroll-period reports, including pending proposals,
  actual or requested timestamps, zone, minutes, totals and pending subtotal. Pending
  values replace that day's recorded values in the export, consistently with the UI.
  Fields are quoted, UTF-8 BOM is included and formula prefixes are neutralized.
- A normal open shift is informational, while successful recording has one success message.
  A calendar's red open-shift dot remains a state indicator.
- Time refresh normally occurs hourly. A background timer wakes every five minutes;
  zones whose providers failed are retried even without user activity. Recovery clears
  the failure flag. Expired samples still cannot be used for punches.

## Google sign-in setup

Create a Google OAuth Web application using Google Cloud Console, with consent screen
and permitted test accounts as needed. Use one origin for browser requests and the API
proxy. Register the exact redirect URI, for example:
`http://localhost:5178/api/auth/google/callback`.

Keep credentials out of Git. Configure the API with:

```
Authentication__Google__ClientId=YOUR_CLIENT_ID
Authentication__Google__ClientSecret=YOUR_CLIENT_SECRET
```

The login page shows Continue with Google only when configured; select a company first.
OAuth state/correlation is managed by ASP.NET Core. Google must return a verified email.
An existing confirmed account, active company, active membership and employee profile
are required. OAuth never creates memberships or assigns roles. The temporary external
cookie lasts five minutes and is removed on completion. A successful login creates the
same server-side session as password login. Production requires HTTPS.
Real Google consent/callback cannot be verified until credentials are supplied.

## Optional LLM proposal (not enabled)

Use a locally hosted model through Ollama for a draft explanation of monthly anomalies:
missing reports, unusually long shifts and pending corrections. Local execution avoids a
per-request API fee, but requires suitable hardware; model licenses must be reviewed.
Do not promise a permanently free hosted API.

Implementation proposal: add `IReportReviewAdvisor` in Application; an Ollama HTTP adapter
in Infrastructure with timeout, cancellation and a disabled-by-default configuration; an
authorized manager endpoint that loads the existing company-scoped report, removes names,
emails and pay, and submits only dates, durations and anomaly flags. Validate a structured
response and display it as a suggestion with the underlying facts. Treat employee notes as
untrusted text. Keep all calculations deterministic and allow no model tools or writes.
The manager approves individual corrections using existing endpoints. Model outage must
leave reports and approval actions fully usable. Test cross-company denial, malformed
output, timeout, prompt injection and no-write guarantees before enabling the feature.

## Further roadmap

Suggested future work: shared session storage and time samples for multiple API instances;
metrics for provider outages and refresh age; optional email invitations; overtime policies,
leave requests and report retention; accessibility and mobile coverage. These are proposals,
not features implemented in this branch.

## Validation

Run from repository root: `dotnet test src/backend/Personal.Attendance.sln`.
Run from `src/frontend/personal-web`: `npm run build`, `npm run typecheck:e2e`,
`npx playwright test --config playwright.personal.config.ts` (isolated browser flows),
and `E2E_BASE_URL=http://127.0.0.1:5178 npm run test:e2e` (real SQL-backed user flows).
The isolated browser suite mocks API responses and Google handoff, not Google itself.
