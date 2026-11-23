# Synchronized Company Time Clock

## Scope

This feature maintains process-local time estimates for configured time zones using external provider samples. The application default is `Asia/Jerusalem`; companies may configure their own zone. The clock is used for live attendance events as well as diagnostics; it does not itself save events or use SQL Server.

## Components

1. `TimeSynchronizationService` is an ASP.NET Core `BackgroundService`. It refreshes once at startup and then on the configured interval. It uses a cancellable `PeriodicTimer`; it does not create a dedicated thread.
2. `TimeSampleCoordinator` serializes refreshes, resolves the configured providers in order, validates a returned sample, and publishes the first valid one. The default order is TimeAPI.io, then Timezone.io.
3. Each typed HTTP provider adapter receives a time-zone ID and returns that zone's local representation, an external UTC instant, daylight-saving state, and measured request duration. `Program.cs` registers each adapter with .NET's `IHttpClientFactory`; registration configures how the client is created and does not send a request. The factory reuses underlying connection handlers instead of opening a new connection pool for every call.
4. `SynchronizedClock` holds one immutable snapshot per registered zone. It anchors each provider instant to `TimeProvider.GetTimestamp()` and calculates elapsed time with `TimeProvider.GetElapsedTime()`. Reads do not call an external provider while that zone's snapshot is fresh.
5. `GET /api/clock/current?timeZoneId={zone}` returns the estimated UTC and local time for the requested zone. If omitted, it uses the configured installation default, initially `Asia/Jerusalem`.
6. If there is no fresh snapshot, the request path invokes the same primary/fallback refresh flow. Diagnostic reads respect the five-minute failure cooldown. Attendance reads use a dedicated method that retries the providers when no fresh snapshot is available, sharing failures for a five-second cooldown; if every configured provider fails or returns invalid time data, the request returns HTTP 503.

## Refresh and fallback

- The service refreshes once at startup and every 60 minutes by default. If the primary provider works, that is about 24 scheduled requests per day per API process, plus the startup request.
- A sample is considered usable for at most 65 minutes by default. This gives a five-minute margin after a scheduled refresh before the clock fails closed.
- Each provider has a 5-second HTTP timeout by default. The timeout bounds how long one unresponsive provider can delay the fallback; with two providers attempted sequentially, one refresh can take up to about 10 seconds if both time out.
- After a failed diagnostic request-path refresh, another diagnostic refresh is throttled for 5 minutes by default. This limits repeated outbound calls during an outage. Attendance fallback uses a shorter five-second cooldown after a failed attempt when there is no fresh snapshot. Concurrent waiters reuse that failure; they return service unavailable instead of each repeating the provider chain.
- A refresh uses the configured order. A network error, timeout, or invalid sample moves to the next provider.
- If a scheduled refresh fails, the previous snapshot remains available only until its configured maximum age.
- Once the snapshot expires, reads fail closed until an external source returns a valid sample. Server, database, and browser wall clocks are not fallback sources.
- A semaphore serializes refresh attempts. Attendance fallback rechecks the snapshot after acquiring the lock, so concurrent requests reuse a sample published by the first request. If providers continue to fail, later attendance attempts can cause additional sequential provider calls because each punch must attempt external time before failing closed.

## Time calculation and accuracy

The returned provider timestamp can have been generated at any point during its HTTP round trip. The clock adds half of the measured request duration as a midpoint estimate, then advances the estimate with monotonic elapsed time. The monotonic source prevents changes to the machine wall clock from changing the elapsed duration. The midpoint assumption is approximate because network delay can be asymmetric and the provider's timestamp-generation instant is not exposed.

## Configuration

`src/backend/Personal.Attendance.Api/appsettings.json` configures the default time zone, provider order, refresh interval, maximum sample age, provider timeout, and failure cooldown. The one-hour interval keeps normal provider traffic low; the five-second timeout bounds how long one unresponsive provider can delay fallback to the next provider. With two providers attempted sequentially, a failed refresh can take up to about ten seconds. Startup validation requires a supported default time zone, distinct non-empty provider identifiers, positive durations, and a maximum sample age that exceeds the refresh interval. Company settings are validated and register each company's zone with the process clock.

## Deployment boundary

The snapshot exists only in one API process. Restarting the process requires a new sample. Multiple API instances maintain separate snapshots and independently call the providers. If deployment scales out, decide whether to share the clock state or accept and monitor per-instance samples before using it for attendance events.

## Endpoint

`GET /api/clock/current` has no attendance-write side effects. The optional `timeZoneId` query parameter selects the time zone, and a successful response contains:

- `utcTime`: the derived instant with UTC offset.
- `localTime`: the same instant converted with the `timeZone` rules.
- `timeZone`: the requested or default time-zone identifier.
- `provider`: the provider that supplied the snapshot.
- `providerSampleUtcTime`: the original external UTC sample.
- `sampleAgeMilliseconds`: monotonic time since the sample was received into this process.
- `providerRequestDurationMilliseconds` and `latencyCompensationMilliseconds`: diagnostic values for understanding the estimate.

If the requested zone's clock is unavailable, the endpoint returns `503 Service Unavailable` with a generic error message. An unsupported zone returns `400 Bad Request`.
