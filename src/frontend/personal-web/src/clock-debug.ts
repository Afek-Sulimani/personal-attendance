import "./clock-debug.css";

type ClockReading = {
  timeZone: string;
  provider: string;
  utcTime: string;
  zurichTime: string;
  providerSampleUtcTime: string;
  sampleAgeMilliseconds: number;
  providerRequestDurationMilliseconds: number;
  latencyCompensationMilliseconds: number;
};

type ProviderSample = {
  provider: string;
  timeZone: string;
  localTime: string;
  utcTime: string;
  utcOffset: string;
  isDaylightSaving: boolean;
  requestDurationMilliseconds: number;
};

const clockStatus = getElement<HTMLSpanElement>("clock-status");
const clockIsrael = getElement<HTMLParagraphElement>("clock-zurich");
const clockUtc = getElement<HTMLElement>("clock-utc");
const clockProvider = getElement<HTMLElement>("clock-provider");
const clockSample = getElement<HTMLElement>("clock-sample");
const clockAge = getElement<HTMLElement>("clock-age");
const clockDuration = getElement<HTMLElement>("clock-duration");
const clockCompensation = getElement<HTMLElement>("clock-compensation");
const clockError = getElement<HTMLParagraphElement>("clock-error");
const refreshClockButton = getElement<HTMLButtonElement>("refresh-clock-button");
const providerSelect = getElement<HTMLSelectElement>("provider-select");
const compareButton = getElement<HTMLButtonElement>("compare-button");
const comparisonResult = getElement<HTMLDivElement>("comparison-result");
const comparisonError = getElement<HTMLParagraphElement>("comparison-error");

let isClockRequestRunning = false;

async function requestClockReading(): Promise<ClockReading> {
  const response = await fetch("/api/clock/current", { cache: "no-store" });
  const body: unknown = await response.json();

  if (!response.ok) {
    throw new Error(getApiError(body));
  }

  if (!isClockReading(body)) {
    throw new Error("The local API returned an invalid clock reading.");
  }

  return body;
}

async function requestProviderSample(providerId: string): Promise<ProviderSample> {
  const response = await fetch(`/api/time-sources/${encodeURIComponent(providerId)}`, { cache: "no-store" });
  const body: unknown = await response.json();

  if (!response.ok) {
    throw new Error(getApiError(body));
  }

  if (!isProviderSample(body)) {
    throw new Error("The provider API returned an invalid time sample.");
  }

  return body;
}

async function updateClockDisplay(): Promise<void> {
  if (isClockRequestRunning) {
    return;
  }

  isClockRequestRunning = true;
  refreshClockButton.disabled = true;
  clockError.hidden = true;
  clockStatus.textContent = "Refreshing…";
  clockStatus.dataset.state = "loading";

  try {
    const reading = await requestClockReading();
    renderClockReading(reading);
    clockStatus.textContent = "Available";
    clockStatus.dataset.state = "available";
  } catch (error) {
    clockStatus.textContent = "Unavailable";
    clockStatus.dataset.state = "unavailable";
    clockError.textContent = getErrorMessage(error);
    clockError.hidden = false;
  } finally {
    isClockRequestRunning = false;
    refreshClockButton.disabled = false;
  }
}

async function compareWithProvider(): Promise<void> {
  compareButton.disabled = true;
  comparisonError.hidden = true;
  comparisonResult.textContent = "Requesting the provider and local clock…";

  try {
    const providerId = providerSelect.value;
    const [providerSample, clockReading] = await Promise.all([
      requestProviderSample(providerId),
      requestClockReading(),
    ]);
    renderComparison(providerSample, clockReading);
  } catch (error) {
    comparisonResult.textContent = "Comparison could not be completed.";
    comparisonError.textContent = getErrorMessage(error);
    comparisonError.hidden = false;
  } finally {
    compareButton.disabled = false;
  }
}

function renderClockReading(reading: ClockReading): void {
  clockIsrael.textContent = formatIsraelTime(reading.utcTime);
  clockUtc.textContent = reading.utcTime;
  clockProvider.textContent = reading.provider;
  clockSample.textContent = reading.providerSampleUtcTime;
  clockAge.textContent = `${reading.sampleAgeMilliseconds.toFixed(0)} ms`;
  clockDuration.textContent = `${reading.providerRequestDurationMilliseconds.toFixed(0)} ms`;
  clockCompensation.textContent = `${reading.latencyCompensationMilliseconds.toFixed(1)} ms`;
}

function renderComparison(sample: ProviderSample, reading: ClockReading): void {
  const differenceMilliseconds = Date.parse(sample.utcTime) - Date.parse(reading.utcTime);
  const differenceSeconds = differenceMilliseconds / 1_000;
  const direction = differenceMilliseconds > 0 ? "ahead" : "behind";

  comparisonResult.replaceChildren();
  appendComparisonRow("Provider", sample.provider);
  appendComparisonRow("Provider time", `${formatIsraelTime(sample.utcTime)} (${sample.utcOffset})`);
  appendComparisonRow("Personal time", formatIsraelTime(reading.utcTime));
  appendComparisonRow("Approximate difference", `${Math.abs(differenceSeconds).toFixed(3)} seconds ${direction}`);
  appendComparisonRow("Provider request", `${sample.requestDurationMilliseconds.toFixed(0)} ms`);
}

function appendComparisonRow(label: string, value: string): void {
  const row = document.createElement("p");
  const labelElement = document.createElement("strong");
  labelElement.textContent = `${label}: `;
  row.append(labelElement, document.createTextNode(value));
  comparisonResult.append(row);
}

function formatIsraelTime(utcInstant: string): string {
  // Parse the API-provided instant and format it explicitly for Israel, independent of browser zone.
  return new Intl.DateTimeFormat("en-GB", {
    timeZone: "Europe/Israel",
    year: "numeric",
    month: "2-digit",
    day: "2-digit",
    hour: "2-digit",
    minute: "2-digit",
    second: "2-digit",
    hourCycle: "h23",
  }).format(new Date(utcInstant));
}

function isClockReading(value: unknown): value is ClockReading {
  return typeof value === "object" && value !== null &&
    "timeZone" in value && typeof value.timeZone === "string" &&
    "provider" in value && typeof value.provider === "string" &&
    "utcTime" in value && typeof value.utcTime === "string" &&
    "zurichTime" in value && typeof value.zurichTime === "string" &&
    "providerSampleUtcTime" in value && typeof value.providerSampleUtcTime === "string" &&
    "sampleAgeMilliseconds" in value && typeof value.sampleAgeMilliseconds === "number" &&
    "providerRequestDurationMilliseconds" in value && typeof value.providerRequestDurationMilliseconds === "number" &&
    "latencyCompensationMilliseconds" in value && typeof value.latencyCompensationMilliseconds === "number";
}

function isProviderSample(value: unknown): value is ProviderSample {
  return typeof value === "object" && value !== null &&
    "provider" in value && typeof value.provider === "string" &&
    "timeZone" in value && typeof value.timeZone === "string" &&
    "localTime" in value && typeof value.localTime === "string" &&
    "utcTime" in value && typeof value.utcTime === "string" &&
    "utcOffset" in value && typeof value.utcOffset === "string" &&
    "isDaylightSaving" in value && typeof value.isDaylightSaving === "boolean" &&
    "requestDurationMilliseconds" in value && typeof value.requestDurationMilliseconds === "number";
}

function getApiError(value: unknown): string {
  return typeof value === "object" && value !== null &&
    "error" in value && typeof value.error === "string"
    ? value.error
    : "The request failed.";
}

function getErrorMessage(error: unknown): string {
  return error instanceof Error ? error.message : "The request failed.";
}

function getElement<T extends HTMLElement>(id: string): T {
  const element = document.getElementById(id);
  if (!(element instanceof HTMLElement)) {
    throw new Error(`Missing debug page element: ${id}`);
  }

  return element as T;
}

compareButton.addEventListener("click", () => void compareWithProvider());
refreshClockButton.addEventListener("click", () => void updateClockDisplay());
void updateClockDisplay();
