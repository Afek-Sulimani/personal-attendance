---
name: personal-code-review
description: Review changed ASP.NET Core, C#, React, and TypeScript code for correctness, security, company isolation, time integrity, accessibility, and maintainability. Use when asked to review a diff, commit, branch, or pull request. Do not modify code.
---

# Personal Code Review

Review code changes only. Do not edit files, run tests, build the project, commit, push, merge, or create pull requests as part of a review. The user must authorize those actions separately.

## Review process

1. Read `CLAUDE.md`, `.claude/CLAUDE.md`, and the relevant architecture/time-provider documents before reviewing behavior.
2. Identify the exact change being reviewed: target branch, commit range, or PR diff. Do not present unrelated uncommitted work as part of the change.
3. Trace affected behavior across React requests, API endpoints, application contracts, domain rules, and infrastructure adapters as applicable.
4. Prioritize exploitable security issues, data corruption, incorrect attendance timestamps, cross-company access, broken authorization, and user-visible failures.
5. Report actionable findings only. Each finding must include a file and line, impact, trigger condition, and a concrete fix direction.
6. Separate confirmed defects from open questions and lower-priority suggestions. Do not invent requirements to justify a finding.
7. Do not claim checks were run. You may inspect existing tests or CI results; do not execute tests unless separately asked.

## Finding priorities

- **P0 — Critical:** Severe exploitable vulnerability, broad company-data exposure, or likely irreversible data corruption.
- **P1 — High:** Serious security, authorization, attendance-time, or correctness defect affecting a meaningful flow.
- **P2 — Medium:** Narrower defect or significant reliability/maintainability problem.
- **P3 — Low:** Small correctness or accessibility issue with limited impact. Avoid reporting personal style preferences as findings.

Avoid duplicate findings for the same root cause. Do not lower a finding's priority to make a report look balanced.

## ASP.NET Core and C# checks

### HTTP boundaries and layers

- Keep controllers focused on HTTP input/output, status codes, and authorization entry points. Put product decisions in Application/Domain code.
- Keep Domain independent of ASP.NET Core, EF Core, transport DTOs, and provider SDKs.
- Check that request/response DTOs expose only fields needed by the caller; do not serialize persistence entities directly.
- Validate request shape and business input at the appropriate boundary. Return consistent client errors without leaking stack traces or internal exception details.
- Propagate `CancellationToken` through asynchronous I/O. Avoid blocking calls such as `.Result` and `.Wait()`; never use `async void` for request or background work.
- Check dependency-injection lifetimes. A singleton must not capture a scoped service such as `DbContext`.
- Use `IHttpClientFactory` or typed clients for outbound HTTP. Set bounded timeouts and handle non-success, malformed, and cancelled requests. Retry only when bounded and safe.

### Security and company isolation

- Enforce authentication and authorization in the API. Hiding a React route or button is not authorization.
- Resolve the active company from the authenticated membership. Do not trust a client-supplied company identifier to grant access.
- Check object-level authorization for each company-scoped employee, report, invitation, or other resource.
- Review cookie, CSRF, CORS, validation, and session behavior when the change affects them.
- For cookie-backed SPA APIs, require antiforgery protection for state-changing requests, generic login failures, bounded login attempts, secure cookie flags, and server-derived company membership.
- Confirm that in-memory authentication tickets are intentionally single-process and that scale-out plans include a shared ticket store and Data Protection keys.
- For attendance writes, reject client-supplied timestamps and employee/company IDs; derive the actor from authenticated claims and recheck the active company membership.
- Confirm each punch uses `ICurrentIsraelClock`, preserves provider/sample provenance, fails closed when no valid external sample exists, and keeps minute truncation in the domain.
- Check that invalid transitions return conflicts and concurrent writes rely on the existing filtered unique index and row-version without opening duplicate shifts.
- Never log or return passwords, session tokens, API keys, connection strings, or unnecessary personal data.
- Check that errors and logs avoid leaking internal exception details or secrets.

### Persistence and reliability

- The approved core schema covers Identity storage, companies, company memberships and roles, employee profiles, and work shifts. Do not add unapproved tables, columns, migrations, or database behaviors.
- Where EF Core code exists, review async query/write use, result projection, read-only no-tracking queries, N+1 risks, transaction boundaries, concurrency, and company scoping.
- For retried commands, review idempotency and duplicate-write behavior. For multi-step changes, check transaction consistency.
- Background work should use host-managed services with cancellation and bounded asynchronous loops, not detached `Task.Run` or manually managed threads.
- Do not use server wall-clock, database time, or browser time as the timestamp for a Clock In/Clock Out event.

### External time integrity

- An attendance timestamp must derive from a validated external API sample for `Asia/Jerusalem`.
- Store event instants in UTC and preserve provider/source provenance; convert for local display and payroll-calendar rules.
- Validate provider zone, timestamp format, offset, daylight-saving behavior, timeouts, and malformed responses.
- If no approved external source is usable, reject the punch rather than falling back to browser, server, or database time.
- Distinguish the current diagnostic provider-check code from any future synchronized clock or attendance write path.

## React and TypeScript checks

### React behavior

- Keep components and Hooks pure during render; event handlers and necessary Effects own side effects.
- Follow the Rules of Hooks. Do not call Hooks conditionally, in loops, or from regular utility functions.
- Prefer derived values over duplicated state. Use Effects only to synchronize with external systems when an event handler is not the right place.
- Review dependency arrays, cleanup, stale responses, cancellation, and race conditions for asynchronous requests.
- Use immutable state updates. Avoid abstractions that make a small feature harder to follow.

### Accessibility and browser security

- Use semantic HTML controls: a real `<button>` for actions and an `<a>` for navigation. Preserve keyboard use and visible focus.
- Provide accessible labels, status announcements, and useful error text for interactive requests.
- Do not put secrets in frontend bundles. Time-provider requests and credentials belong on the server.
- Avoid unsafe HTML injection such as unjustified `dangerouslySetInnerHTML`.
- Client-side route visibility is not an authorization boundary. Do not treat UI state as proof of identity or permission.
- Attendance time must come from the backend response; do not create it with the browser's `Date` clock.

### TypeScript and user feedback

- Keep API request/response types explicit. Avoid `any` unless there is a documented reason.
- Handle loading, success, empty, and error states for network-backed interactions.
- Prevent duplicate submissions where they can create duplicate server actions; use server-side idempotency for correctness.
- Check that labels, errors, and status updates are understandable and that the layout works on narrow screens.

## Required response format

Return the most important findings first:

```text
## Findings
- [P1] <Short actionable title> — `path/to/file.ts:42`
  Impact: <What can go wrong and when>
  Fix: <Small, concrete change>

## Open questions
- <Only questions whose answer could change correctness or security>

## Summary
<One or two sentences. If there are no actionable findings, say so plainly.>
```

If there are no findings, say: `No actionable findings in the reviewed diff.` Do not pad the review with praise or a list of everything that looked fine.

## Official references

- ASP.NET Core best practices: https://learn.microsoft.com/en-us/aspnet/core/fundamentals/best-practices?view=aspnetcore-10.0
- ASP.NET Core HttpClientFactory: https://learn.microsoft.com/en-us/aspnet/core/fundamentals/http-requests?view=aspnetcore-10.0
- ASP.NET Core security: https://learn.microsoft.com/en-us/aspnet/core/security/
- React Rules: https://react.dev/reference/rules
- React event handlers: https://react.dev/learn/responding-to-events
- React Effects: https://react.dev/learn/you-might-not-need-an-effect
- React state structure: https://react.dev/learn/choosing-the-state-structure
