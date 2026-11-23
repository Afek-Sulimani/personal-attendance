# Temporary Time Provider Check

**Status:** The individual provider endpoints remain diagnostics. The synchronized clock currently uses TimeAPI.io first and Timezone.io as fallback. The attendance API uses the synchronized clock and records approved punches in the existing SQL Server shift table.

## Providers queried by the diagnostic page

| Display name | Provider URL |
|---|---|
| TimeAPI.io | `https://timeapi.io/api/Time/current/zone?timeZone={timeZoneId}` |
| Timezone.io | `https://worldtime.timezone.io/api/timezone/{timeZoneId}` |

When `timeZoneId` is omitted from the local endpoint, the API uses its configured default, initially `Asia/Jerusalem`.

The second URL is Timezone.io's WorldTimeAPI-compatible endpoint. The retired `worldtimeapi.org` host is not used. Timezone.io documents rate limits and does not offer an uptime guarantee; treat it as a test candidate, not a production commitment.

## Request flow

1. The user clicks one provider button.
2. React sends `GET /api/time-sources/{sourceId}` to the local ASP.NET Core API.
3. The API selects an allowed provider adapter and sends an HTTPS request.
4. The adapter checks that the response identifies the requested time zone, parses the date/time, and normalizes it to an offset-bearing local time and UTC instant.
5. The API returns the provider, local time, UTC instant, offset, daylight-saving flag, and request duration.
6. React displays that response. It is transient display state only; the endpoint does not save an authoritative clock sample or attendance event.

The providers are queried separately. A failure from one button does not automatically call the other provider.

## Synchronized clock diagnostic

The API has a hosted background service that acquires an external sample at startup and every hour. A singleton in the API process holds an immutable snapshot:

- Provider identifier
- Validated external UTC anchor
- Monotonic timestamp captured for the sample
- Time-zone identifier
- Sample identifier and measured request duration

The current estimate is the external UTC anchor plus monotonic elapsed time. It does not use the browser clock, server wall clock, or database clock. On restart, the in-memory snapshot is unavailable until a fresh provider sample succeeds. A sample expires after 65 minutes; if no valid source returns a newer sample, the clock endpoint fails closed with HTTP 503. This cadence is a project default for the diagnostic feature, not an attendance or payroll guarantee.

This file documents the time-provider diagnostic behavior, not the attendance workflow or relational schema. The punch API is described in [ATTENDANCE_PUNCH_DESIGN.md](ATTENDANCE_PUNCH_DESIGN.md), and the core shift table and its external-source fields are documented in [DATABASE_DESIGN.md](../DATABASE_DESIGN.md).
