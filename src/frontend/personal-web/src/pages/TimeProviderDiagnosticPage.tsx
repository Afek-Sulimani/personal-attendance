import LoadingIndicator from "../components/LoadingIndicator";
import { useState } from "react";

type ProviderId = "timeapi-io" | "timezone-io";

type TimeSample = {
  provider: string;
  timeZone: string;
  localTime: string;
  utcTime: string;
  utcOffset: string;
  isDaylightSaving: boolean;
  requestDurationMilliseconds: number;
};

type ProviderState = {
  loading: boolean;
  sample?: TimeSample;
  error?: string;
};

function isTimeSample(value: unknown): value is TimeSample {
  return typeof value === "object" &&
    value !== null &&
    "provider" in value && typeof value.provider === "string" &&
    "timeZone" in value && typeof value.timeZone === "string" &&
    "localTime" in value && typeof value.localTime === "string" &&
    "utcTime" in value && typeof value.utcTime === "string" &&
    "utcOffset" in value && typeof value.utcOffset === "string" &&
    "isDaylightSaving" in value && typeof value.isDaylightSaving === "boolean" &&
    "requestDurationMilliseconds" in value &&
    typeof value.requestDurationMilliseconds === "number";
}

function getApiError(value: unknown): string {
  return typeof value === "object" && value !== null && "error" in value &&
    typeof value.error === "string"
    ? value.error
    : "The request failed.";
}

const providers: { id: ProviderId; name: string; endpoint: string }[] = [
  {
    id: "timeapi-io",
    name: "TimeAPI.io",
    endpoint: "GET https://timeapi.io/api/Time/current/zone?timeZone=Asia/Jerusalem",
  },
  {
    id: "timezone-io",
    name: "Timezone.io",
    endpoint: "GET https://worldtime.timezone.io/api/timezone/Asia/Jerusalem",
  },
];

export default function TimeProviderDiagnosticPage() {
  const [states, setStates] = useState<Record<ProviderId, ProviderState>>({
    "timeapi-io": { loading: false },
    "timezone-io": { loading: false },
  });

  async function requestTime(providerId: ProviderId) {
    setStates(current => ({ ...current, [providerId]: { loading: true } }));

    try {
      const response = await fetch(`/api/time-sources/${providerId}`);
      const body: unknown = await response.json();
      if (!response.ok) throw new Error(getApiError(body));
      if (!isTimeSample(body)) throw new Error("The API returned an invalid time response.");

      setStates(current => ({ ...current, [providerId]: { loading: false, sample: body } }));
    } catch (error) {
      setStates(current => ({
        ...current,
        [providerId]: {
          loading: false,
          error: error instanceof Error ? error.message : "The request failed.",
        },
      }));
    }
  }

  return (
    <main className="page">
      <header className="page-header">
        <div>
          <p className="eyebrow">TEMPORARY API CHECK</p>
          <h1>Israel time providers</h1>
          <p className="intro">
            This is a temporary diagnostic screen. It is not the final Personal product design.
            Each button requests current time through the local backend. No attendance record is saved.
          </p>
        </div>
        <span className="zone-badge">Asia/Jerusalem</span>
      </header>

      <section className="provider-grid" aria-label="Time provider checks">
        {providers.map(provider => {
          const state = states[provider.id];
          return (
            <article className="provider-card" key={provider.id}>
              <div className="provider-card-header">
                <div>
                  <p className="eyebrow">TIME SOURCE</p>
                  <h2>{provider.name}</h2>
                </div>
                <span className="provider-mark" aria-hidden="true">↗</span>
              </div>
              <code className="endpoint">{provider.endpoint}</code>
              <button aria-busy={state.loading}
                className="request-button"
                onClick={() => void requestTime(provider.id)}
                disabled={state.loading}
              >
                {state.loading ? <LoadingIndicator label="Requesting…" /> : "Request Israel time"}
              </button>
              {state.error && (
                <div className="result error" role="alert">
                  <strong>Request failed</strong><span>{state.error}</span>
                </div>
              )}
              {state.sample && (
                <div className="result" aria-live="polite">
                  <p className="result-label">Israel local time</p>
                  <strong className="time-value">{state.sample.localTime}</strong>
                  <dl>
                    <div><dt>UTC instant</dt><dd>{state.sample.utcTime}</dd></div>
                    <div><dt>UTC offset</dt><dd>{state.sample.utcOffset}</dd></div>
                    <div><dt>Daylight saving</dt><dd>{state.sample.isDaylightSaving ? "Active" : "Inactive"}</dd></div>
                    <div><dt>Request duration</dt><dd>{state.sample.requestDurationMilliseconds.toFixed(0)} ms</dd></div>
                  </dl>
                </div>
              )}
            </article>
          );
        })}
      </section>

      <footer className="explanation">
        <strong>What this check does</strong>
        <p>
          The browser calls Personal&apos;s local API. The API calls the selected external provider,
          validates that it returned Asia/Jerusalem, and sends the result back for display.
          These buttons test each provider independently; they do not save attendance events.
        </p>
      </footer>
    </main>
  );
}
