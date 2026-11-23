import { apiRequest } from "../lib/apiClient";
import LoadingIndicator from "../components/LoadingIndicator";
import { useCallback, useEffect, useState } from "react";
import type { FormEvent } from "react";
import { useNavigate } from "react-router-dom";
import ApplicationLayout from "../components/ApplicationLayout";
import { getCurrentUser, signOut } from "../features/auth/authApi";
import type { AuthenticatedUser } from "../features/auth/authApi";
import {
  getCompanyPayrollSettings,
  saveCompanyPayrollSettings,
} from "../features/payroll/payrollApi";
import type { CompanyPayrollSettings } from "../features/payroll/payrollApi";
import { ApiRequestError } from "../lib/apiClient";

const weekdays = [
  ["Sunday", "Sunday"],
  ["Monday", "Monday"],
  ["Tuesday", "Tuesday"],
  ["Wednesday", "Wednesday"],
  ["Thursday", "Thursday"],
  ["Friday", "Friday"],
  ["Saturday", "Saturday"],
] as const;

function settingsToForm(settings: CompanyPayrollSettings) {
  return {
    payrollPeriodStartDay: settings.payrollPeriodStartDay?.toString() ?? "",
    timeZoneId: settings.timeZoneId || "Asia/Jerusalem",
    defaultWorkStartTime: settings.defaultWorkStartTime ?? "",
    defaultWorkEndTime: settings.defaultWorkEndTime ?? "",
    defaultWorkDays: new Set(settings.defaultWorkDays),
  };
}

export default function CompanyPayrollSettingsPage() {
  const navigate = useNavigate();
  const [user, setUser] = useState<AuthenticatedUser | null>(null);
  const [payrollPeriodStartDay, setPayrollPeriodStartDay] = useState("");
  const [timeZones, setTimeZones] = useState<{id: string; label: string}[]>([]);
  useEffect(() => {
    const controller = new AbortController();
    apiRequest<{id: string; label: string}[]>("/api/time-zones", {signal: controller.signal}).then(setTimeZones).catch(error => { if (!controller.signal.aborted) setErrorMessage(String(error)); });
    return () => controller.abort();
  }, []);
  const [timeZoneId, setTimeZoneId] = useState("Asia/Jerusalem");
  const [defaultWorkStartTime, setDefaultWorkStartTime] = useState("");
  const [defaultWorkEndTime, setDefaultWorkEndTime] = useState("");
  const [defaultWorkDays, setDefaultWorkDays] = useState<Set<string>>(new Set());
  const [isConfigured, setIsConfigured] = useState(false);
  const [isLoading, setIsLoading] = useState(true);
  const [isSaving, setIsSaving] = useState(false);
  const [isSigningOut, setIsSigningOut] = useState(false);
  const [errorMessage, setErrorMessage] = useState("");
  const [statusMessage, setStatusMessage] = useState("");

  const handleError = useCallback((error: unknown) => {
    if (error instanceof ApiRequestError && error.status === 401) {
      navigate("/login", { replace: true });
      return;
    }
    setErrorMessage(error instanceof Error ? error.message : "Company payroll settings could not be loaded.");
  }, [navigate]);

  useEffect(() => {
    const controller = new AbortController();
    let isCurrent = true;

    async function loadPage() {
      try {
        const [currentUser, settings] = await Promise.all([
          getCurrentUser(controller.signal),
          getCompanyPayrollSettings(controller.signal),
        ]);
        if (!isCurrent) return;
        setUser(currentUser);
        const form = settingsToForm(settings);
        setPayrollPeriodStartDay(form.payrollPeriodStartDay);
        setTimeZoneId(form.timeZoneId);
        setDefaultWorkStartTime(form.defaultWorkStartTime);
        setDefaultWorkEndTime(form.defaultWorkEndTime);
        setDefaultWorkDays(form.defaultWorkDays);
        setIsConfigured(settings.isConfigured);
      } catch (error) {
        if (isCurrent) handleError(error);
      } finally {
        if (isCurrent) setIsLoading(false);
      }
    }

    void loadPage();
    return () => {
      isCurrent = false;
      controller.abort();
    };
  }, [handleError]);

  function toggleWeekday(day: string) {
    setDefaultWorkDays(current => {
      const next = new Set(current);
      if (next.has(day)) next.delete(day);
      else next.add(day);
      return next;
    });
  }

  async function handleSave(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setIsSaving(true);
    setErrorMessage("");
    setStatusMessage("");
    try {
      const settings = await saveCompanyPayrollSettings({
        payrollPeriodStartDay: Number(payrollPeriodStartDay),
        timeZoneId: timeZoneId.trim(),
        defaultWorkStartTime,
        defaultWorkEndTime,
        defaultWorkDays: weekdays.map(([name]) => name).filter(name => defaultWorkDays.has(name)),
      });
      setIsConfigured(settings.isConfigured);
      setStatusMessage("Company payroll settings saved.");
    } catch (error) {
      handleError(error);
    } finally {
      setIsSaving(false);
    }
  }

  async function handleSignOut() {
    setIsSigningOut(true);
    try {
      await signOut();
      navigate("/login", { replace: true });
    } catch (error) {
      handleError(error);
    } finally {
      setIsSigningOut(false);
    }
  }

  if (isLoading || !user) {
    return <main className="attendance-loading-page"><p role="status">{errorMessage || <LoadingIndicator label="Loading company settings…" />}</p></main>;
  }

  return (
    <ApplicationLayout user={user} isSigningOut={isSigningOut} onSignOut={() => void handleSignOut()}>
      <div className="attendance-page-content">
        <header className="attendance-page-heading">
          <div>
            <p className="attendance-eyebrow">COMPANY SETUP</p>
            <h1>Payroll and work settings</h1>
            <p className="attendance-page-description">Set the company payroll cycle, time zone, and standard work schedule.</p>
          </div>
          <span className="attendance-timezone-badge">{timeZoneId}</span>
        </header>

        {!isConfigured && (
          <div className="status-banner information" role="status">
            Complete and save these settings before employees can clock in. The initial time zone is Asia/Jerusalem.
          </div>
        )}
        {errorMessage && <div className="status-banner error" role="alert">{errorMessage}</div>}
        {statusMessage && <div className="status-banner success" role="status">{statusMessage}</div>}

        <form className="payroll-workday-card company-settings-form" onSubmit={event => void handleSave(event)}>
          <div className="settings-form-grid">
            <label className="form-field">
              <span>Payroll period starts on day</span>
              <input
                type="number"
                min="1"
                max="31"
                step="1"
                required
                value={payrollPeriodStartDay}
                onChange={event => setPayrollPeriodStartDay(event.target.value)}
                aria-describedby="payroll-period-hint"
              />
              <small id="payroll-period-hint">The start day sets the company's monthly payroll cycle.</small>
            </label>

            <label className="form-field">
              <span>Company time zone</span>
              <input
                type="text"
                required
                maxLength={100}
                value={timeZoneId}
                onChange={event => setTimeZoneId(event.target.value)}
                placeholder="Search country or city"
                list="company-time-zones"
              />
              <datalist id="company-time-zones">{timeZones.map(zone => <option key={zone.id} value={zone.id}>{zone.label}</option>)}</datalist>
              <small>Use a supported time zone ID. Historical punches keep the time zone saved with each punch.</small>
            </label>

            <label className="form-field">
              <span>Standard work start time</span>
              <input
                type="time"
                step="60"
                required
                value={defaultWorkStartTime}
                onChange={event => setDefaultWorkStartTime(event.target.value)}
              />
            </label>

            <label className="form-field">
              <span>Standard work end time</span>
              <input
                type="time"
                step="60"
                required
                value={defaultWorkEndTime}
                onChange={event => setDefaultWorkEndTime(event.target.value)}
              />
            </label>
          </div>

          <fieldset className="company-weekdays-fieldset">
            <legend>Default company workdays</legend>
            <p>These days apply when Payroll has not saved a custom monthly calendar for an employee.</p>
            <div className="company-weekdays-grid">
              {weekdays.map(([name, label]) => (
                <label className="company-weekday-option" key={name}>
                  <input
                    type="checkbox"
                    checked={defaultWorkDays.has(name)}
                    onChange={() => toggleWeekday(name)}
                  />
                  <span>{label}</span>
                </label>
              ))}
            </div>
          </fieldset>

          <button aria-busy={isSaving} className="primary-attendance-button" type="submit" disabled={isSaving}>
            {isSaving ? <LoadingIndicator label="Saving settings…" /> : isConfigured ? "Save company settings" : "Complete company setup"}
          </button>
        </form>
      </div>
    </ApplicationLayout>
  );
}
