---
name: personal-dotnet-unit-tests
description: Add, update, or run deterministic .NET unit tests for Personal Attendance. Use when a task asks for tests or verification of backend behavior.
---

# Personal .NET Unit Tests

## Scope

Use this skill for unit tests of backend behavior. Keep tests local and deterministic. Do not call live time providers, connect to SQL Server, send email, or rely on the machine's current wall clock.

## Test conventions

- Use MSTest and place tests in `src/backend/Personal.Attendance.Tests/` by feature.
- Use fixed UTC timestamps with an explicit `Z` suffix.
- Use `FakeTimeProvider` from `Microsoft.Extensions.TimeProvider.Testing` when behavior depends on elapsed time or expiration.
- Replace external interfaces such as `ICurrentTimeSource` with small fakes that return a known result or a controlled failure.
- Test observable behavior: valid results, rejection/failure cases, fallback order, cancellation, and cache freshness.
- Give each test a name that states the behavior and expected outcome.
- Keep production logic out of test helpers. Avoid reflection and shared mutable fixtures.
- Do not introduce database schemas or provider integration tests in this project.

## Run

From the repository root, run:

```bash
dotnet test src/backend/Personal.Attendance.sln
```

If `dotnet` is not on `PATH`, try `~/.dotnet/dotnet test src/backend/Personal.Attendance.sln`.

Report the test command and its result. Never claim tests passed unless the command completed successfully.
