import LoadingIndicator from "../components/LoadingIndicator";
import { useCallback, useEffect, useState } from "react";
import type { FormEvent } from "react";
import { useNavigate } from "react-router-dom";
import ApplicationLayout from "../components/ApplicationLayout";
import AttendanceCalendar from "../features/attendance/AttendanceCalendar";
import {
  clockInNow,
  clockOutNow,
  getCurrentAttendance,
  getMonthlyAttendanceCalendar,
} from "../features/attendance/attendanceApi";
import type {
  AttendanceCalendar as AttendanceCalendarData,
  AttendanceCalendarDay,
  AttendancePunchResult,
  AttendanceState,
} from "../features/attendance/attendanceApi";
import { submitAttendanceCorrection } from "../features/attendance/correctionApi";
import { getCurrentUser, signOut } from "../features/auth/authApi";
import type { AuthenticatedUser } from "../features/auth/authApi";
import { ApiRequestError } from "../lib/apiClient";

function formatDate(date: string): string {
  return new Intl.DateTimeFormat("en-US", {
    dateStyle: "full",
    timeZone: "UTC",
  }).format(new Date(`${date}T12:00:00.000Z`));
}

// Format the server-returned instant with its saved company zone instead of the browser's own zone.
function formatLocalTime(value: string, timeZoneId: string): string {
  return new Intl.DateTimeFormat("en-GB", {
    dateStyle: "medium",
    timeStyle: "short",
    timeZone: timeZoneId,
  }).format(new Date(value));
}

function normalizeMonth(year: number, month: number, delta: -1 | 1) {
  const nextMonth = new Date(Date.UTC(year, month - 1 + delta, 1));
  return { year: nextMonth.getUTCFullYear(), month: nextMonth.getUTCMonth() + 1 };
}

// Render the API's current company-local time; do not derive an attendance time from the browser clock.
function getCurrentCompanyClockTime(calendar: AttendanceCalendarData): string {
  return new Intl.DateTimeFormat("en-GB", {
    hour: "2-digit",
    minute: "2-digit",
    hourCycle: "h23",
    timeZone: calendar.timeZoneId,
  }).format(new Date(calendar.currentLocalTime));
}

function getStatusText(day: AttendanceCalendarDay): string {
  switch (day.status) {
    case "Complete": return "Clock in and clock out are complete.";
    case "Open": return "This shift is still open.";
    case "Missing": return "This scheduled workday has no attendance record.";
    case "Future": return "This date is in the future.";
    case "NonWorking": return "This is not a scheduled workday.";
    case "NoPunchToday": return "No attendance punch has been recorded today.";
    case "CorrectionPending": return "A correction request is waiting for manager approval.";
    case "CorrectionRejected": return "A correction request was rejected. You can submit a new request.";
    case "ScheduleNotConfigured": return "Payroll has not configured this month's work schedule.";
  }
}

type CorrectionMode = "FullShift" | "ClockInOnly" | "ClockOutOnly";

// Choose which past or current punch fields can be corrected for the selected calendar day.
function getCorrectionMode(day: AttendanceCalendarDay, todayLocalDate: string): CorrectionMode | null {
  if (day.status === "Open" && day.date < todayLocalDate) return "ClockOutOnly";
  if (day.date === todayLocalDate &&
      (day.status === "NoPunchToday" || day.status === "CorrectionRejected" || day.status === "CorrectionPending")) return "ClockInOnly";
  if (day.date < todayLocalDate &&
      (day.status === "Missing" || day.status === "CorrectionRejected" || day.status === "CorrectionPending")) return "FullShift";
  return null;
}

function TimeWheel({
  label,
  value,
  onChange,
}: {
  label: string;
  value: string;
  onChange: (value: string) => void;
}) {
  // Split the controlled value so each wheel can update its own part without losing the other part.
  const [hour = "09", minute = "00"] = value.split(":");

  return (
    <fieldset className="time-wheel-fieldset">
      <legend>{label}</legend>
      <div className="time-wheel-row">
        <label>
          <span>Hour</span>
          <select
            value={hour}
            aria-label={`${label} hour`}
            onChange={event => onChange(`${event.target.value}:${minute}`)}
          >
            {/* Generate all valid 24-hour values instead of relying on locale-specific text input. */}
            {Array.from({ length: 24 }, (_, value) => String(value).padStart(2, "0"))
              .map(option => <option key={option} value={option}>{option}</option>)}
          </select>
        </label>
        <span className="time-wheel-separator" aria-hidden="true">:</span>
        <label>
          <span>Minute</span>
          <select
            value={minute}
            aria-label={`${label} minute`}
            onChange={event => onChange(`${hour}:${event.target.value}`)}
          >
            {/* Generate minute choices at minute precision for historical correction requests. */}
            {Array.from({ length: 60 }, (_, value) => String(value).padStart(2, "0"))
              .map(option => <option key={option} value={option}>{option}</option>)}
          </select>
        </label>
      </div>
    </fieldset>
  );
}

function CorrectionRequestForm({
  day,
  todayLocalDate,
  timeZoneId,
  isSubmitting,
  onSubmit,
}: {
  day: AttendanceCalendarDay;
  todayLocalDate: string;
  timeZoneId: string;
  isSubmitting: boolean;
  onSubmit: (request: {
    workDate: string;
    clockInLocalTime?: string;
    clockOutLocalTime?: string;
    clockOutNextDay: boolean;
  }) => void;
}) {
  const [clockInTime, setClockInTime] = useState("09:00");
  const [clockOutTime, setClockOutTime] = useState("17:00");
  const mode = getCorrectionMode(day, todayLocalDate);
  // A pending request changes the button label because submitting again replaces that request.
  const hasPendingCorrection = day.correctionStatus === "PendingApproval";

  useEffect(() => {
    setClockInTime("09:00");
    setClockOutTime("17:00");
  }, [day.date]);

  if (!mode || day.isWorkDay !== true) return null;

  // Convert wheel selections into the correction API contract, including overnight exit intent.
  function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    const request: {
      workDate: string;
      clockInLocalTime?: string;
      clockOutLocalTime?: string;
      clockOutNextDay: boolean;
    } = {
      workDate: day.date,
      clockOutNextDay: mode !== "ClockInOnly" && clockOutTime <= (
        mode === "ClockOutOnly" && day.clockInAtLocal
          ? new Intl.DateTimeFormat("en-GB", { hour: "2-digit", minute: "2-digit", hourCycle: "h23", timeZone: timeZoneId })
            .format(new Date(day.clockInAtLocal))
          : clockInTime
      ),
    };
    if (mode !== "ClockOutOnly") request.clockInLocalTime = `${clockInTime}:00.0000000`;
    if (mode !== "ClockInOnly") request.clockOutLocalTime = `${clockOutTime}:00.0000000`;
    onSubmit(request);
  }

  return (
    <form
      className="correction-request-form"
      onSubmit={handleSubmit}
    >
      <div className="correction-form-heading">
        <h3>{mode === "ClockOutOnly" ? "Correct clock out" : "Correct attendance time"}</h3>
      </div>

      {mode !== "ClockOutOnly" && (
      <TimeWheel label={`Clock in · ${timeZoneId}`} value={clockInTime} onChange={setClockInTime} />
      )}
      {mode !== "ClockInOnly" && (
        <TimeWheel label={`Clock out · ${timeZoneId}`} value={clockOutTime} onChange={setClockOutTime} />
      )}

      <button aria-busy={isSubmitting} className="secondary-attendance-button" type="submit" disabled={isSubmitting}>
        {isSubmitting ? <LoadingIndicator label="Sending request…" /> : hasPendingCorrection ? "Replace pending request" : "Send for manager approval"}
      </button>
    </form>
  );
}

export default function AttendancePage() {
  const navigate = useNavigate();
  // Keep authenticated identity, server attendance state, and calendar data separate in React state.
  const [user, setUser] = useState<AuthenticatedUser | null>(null);
  const [calendar, setCalendar] = useState<AttendanceCalendarData | null>(null);
  const [attendance, setAttendance] = useState<AttendanceState | null>(null);
  const [selectedDate, setSelectedDate] = useState("");
  const [isLoading, setIsLoading] = useState(true);
  const [isCalendarLoading, setIsCalendarLoading] = useState(false);
  const [activeAction, setActiveAction] = useState<"ClockIn" | "ClockOut" | "Correction" | null>(null);
  const [isSigningOut, setIsSigningOut] = useState(false);
  const [errorMessage, setErrorMessage] = useState("");
  const [statusMessage, setStatusMessage] = useState("");

  // Return to login for an expired session; show other API failures on this page.
  const handleAuthError = useCallback((error: unknown) => {
    if (error instanceof ApiRequestError && error.status === 401) {
      navigate("/login", { replace: true });
      return;
    }
    setErrorMessage(error instanceof Error ? error.message : "Attendance could not be loaded.");
  }, [navigate]);

  // Load a server-built month and preserve the selected day when it still belongs to that month.
  const loadMonth = useCallback(async (year?: number, month?: number) => {
    setIsCalendarLoading(true);
    setErrorMessage("");
    try {
      const nextCalendar = await getMonthlyAttendanceCalendar(year, month);
      setCalendar(nextCalendar);
      // Keep a valid selection on same-month refreshes; otherwise start at day one of the new month.
      setSelectedDate(current => {
        const date = new Date(`${current}T12:00:00.000Z`);
        return current && date.getUTCFullYear() === nextCalendar.year &&
          date.getUTCMonth() + 1 === nextCalendar.month
          ? current
          : `${nextCalendar.year}-${String(nextCalendar.month).padStart(2, "0")}-01`;
      });
    } catch (error) {
      handleAuthError(error);
    } finally {
      setIsCalendarLoading(false);
    }
  }, [handleAuthError]);

  // Refresh open-shift state after a punch and when the page returns to the foreground.
  const refreshCurrentAttendance = useCallback(async () => {
    try {
      const currentAttendance = await getCurrentAttendance();
      setAttendance(currentAttendance);
    } catch (error) {
      handleAuthError(error);
    }
  }, [handleAuthError]);

  useEffect(() => {
    // Cancel initial API requests if the user leaves this page before they finish.
    const controller = new AbortController();
    let isCurrent = true;

    async function loadPage() {
      try {
        // Fetch identity, open-shift state, and calendar together to avoid sequential page startup requests.
        const [currentUser, currentAttendance, nextCalendar] = await Promise.all([
          getCurrentUser(controller.signal),
          getCurrentAttendance(controller.signal),
          getMonthlyAttendanceCalendar(undefined, undefined, controller.signal),
        ]);
        // Ignore a response from an effect that was cleaned up during navigation.
        if (!isCurrent) return;
        setUser(currentUser);
        setAttendance(currentAttendance);
        setCalendar(nextCalendar);
        setSelectedDate(nextCalendar.todayLocalDate);
      } catch (error) {
        // Do not show stale errors after the page has already been left.
        if (!isCurrent) return;
        if (error instanceof ApiRequestError && error.status === 401) {
          navigate("/login", { replace: true });
        } else {
          setErrorMessage(error instanceof Error ? error.message : "Attendance could not be loaded.");
        }
      } finally {
        if (isCurrent) setIsLoading(false);
      }
    }

    void loadPage();
    // Mark the request stale before aborting fetches during unmount or effect replacement.
    return () => {
      isCurrent = false;
      controller.abort();
    };
  }, [navigate]);

  useEffect(() => {
    // Avoid duplicate polling while an action is being recorded or before the calendar is ready.
    if (!calendar || activeAction !== null) return;
    let refreshing = false;
    const refresh = async () => {
      // Skip hidden tabs and overlapping polls to reduce unnecessary backend requests.
      if (document.visibilityState === "hidden" || refreshing) return;
      refreshing = true;
      try {
        await Promise.all([refreshCurrentAttendance(), loadMonth(calendar.year, calendar.month)]);
      } finally { refreshing = false; }
    };
    const onVisibility = () => { void refresh(); };
    // Refresh once per minute and immediately when the user focuses or reopens the tab.
    const timer = window.setInterval(onVisibility, 60_000);
    window.addEventListener("focus", onVisibility);
    document.addEventListener("visibilitychange", onVisibility);
    // Remove timers and event handlers when dependencies change or the page unmounts.
    return () => {
      window.clearInterval(timer);
      window.removeEventListener("focus", onVisibility);
      document.removeEventListener("visibilitychange", onVisibility);
    };
  }, [calendar?.year, calendar?.month, activeAction, loadMonth, refreshCurrentAttendance]);

  async function handlePunch(action: "ClockIn" | "ClockOut") {
    // Lock the visible action and clear old feedback while the server decides the punch time.
    setActiveAction(action);
    setErrorMessage("");
    setStatusMessage("");
    try {
      // Send only the selected action; neither the browser time nor an employee ID is sent as punch data.
      const result: AttendancePunchResult = action === "ClockIn"
        ? await clockInNow()
        : await clockOutNow();
      // Display the local time returned by the API, then reload both state and calendar from the server.
      setStatusMessage(`${result.status === "ClockedIn" ? "Clock In" : "Clock Out"} recorded at ${formatLocalTime(result.occurredAtLocal, result.timeZone)} ${result.timeZone}.`);
      await Promise.all([refreshCurrentAttendance(), loadMonth(calendar?.year, calendar?.month)]);
    } catch (error) {
      handleAuthError(error);
    } finally {
      // Re-enable page actions after either a successful response or an error.
      setActiveAction(null);
    }
  }

  async function handleCorrectionSubmit(
    request: {
      workDate: string;
      clockInLocalTime?: string;
      clockOutLocalTime?: string;
      clockOutNextDay: boolean;
    },
  ) {
    if (!calendar || !selectedDay) return;

    setActiveAction("Correction");
    setErrorMessage("");
    setStatusMessage("");
    try {
      const replacesPendingRequest = selectedDay.correctionStatus === "PendingApproval";
      await submitAttendanceCorrection(request);
      setStatusMessage(replacesPendingRequest
        ? "Your latest correction request replaced the previous request and was sent for manager approval."
        : "Your correction request was sent for manager approval.");
      await loadMonth(calendar.year, calendar.month);
    } catch (error) {
      handleAuthError(error);
    } finally {
      setActiveAction(null);
    }
  }

  async function handleSignOut() {
    setIsSigningOut(true);
    try {
      await signOut();
      navigate("/login", { replace: true });
    } catch (error) {
      handleAuthError(error);
    } finally {
      setIsSigningOut(false);
    }
  }

  // Derive button eligibility from server-provided day and shift state, not from a client-side clock.
  const selectedDay = calendar?.days.find(day => day.date === selectedDate) ?? null;
  const isTodaySelected = calendar?.todayLocalDate === selectedDate;
  // These UI gates improve feedback; AttendanceService repeats every business check on the server.
  const canClockIn = Boolean(
    calendar?.scheduleConfigured &&
    isTodaySelected &&
    selectedDay?.isWorkDay === true &&
    (selectedDay.status === "NoPunchToday" || selectedDay.status === "CorrectionRejected") &&
    !attendance?.hasOpenShift &&
    activeAction === null,
  );
  const canClockOut = Boolean(isTodaySelected && attendance?.hasOpenShift && activeAction === null);

  if (isLoading || !user || !calendar || !attendance) {
    return (
      <main className="attendance-loading-page">
        <p role="status">{errorMessage || <LoadingIndicator label="Loading your attendance…" />}</p>
      </main>
    );
  }

  return (
    <ApplicationLayout user={user} isSigningOut={isSigningOut} onSignOut={() => void handleSignOut()}>
      <div className="attendance-page-content">
        <header className="attendance-page-heading">
          <div>
            <p className="attendance-eyebrow">EMPLOYEE WORKSPACE</p>
            <h1>My attendance</h1>
            <p className="attendance-page-description">Track your workdays and record attendance.</p>
          </div>
          {calendar.timeZoneId?.trim() && (
            <span className="attendance-timezone-badge">{calendar.timeZoneId}</span>
          )}
        </header>

        {errorMessage && <div className="status-banner error" role="alert">{errorMessage}</div>}
        {statusMessage && <div className="status-banner success" role="status">{statusMessage}</div>}

        {!calendar.scheduleConfigured && (
          <div className="status-banner information" role="status">
            Payroll has not configured the work schedule for this month. Clock In is unavailable until it is set.
          </div>
        )}

        <div className="attendance-workspace-grid">
          <AttendanceCalendar
            calendar={calendar}
            selectedDate={selectedDate}
            onSelectDate={setSelectedDate}
            onChangeMonth={direction => {
              const target = normalizeMonth(calendar.year, calendar.month, direction);
              void loadMonth(target.year, target.month);
            }}
            isLoading={isCalendarLoading}
          />

          <section className="attendance-day-panel" aria-label="Selected attendance date">
            <p className="attendance-eyebrow">SELECTED DAY</p>
            <h2>{selectedDay ? formatDate(selectedDay.date) : "Select a date"}</h2>
            {selectedDay && (
              <>
                <p className={`attendance-day-status status-${selectedDay.status === "Open" ? "information" : selectedDay.status.toLowerCase()}`}>
                  {selectedDay.status === "Open" ? "Clocked in · shift in progress" : getStatusText(selectedDay)}
                </p>
                <dl className="attendance-day-times">
                  {selectedDay.clockInAtLocal && selectedDay.clockInTimeZoneId && (
                    <div><dt>Clock in · {selectedDay.clockInTimeZoneId}</dt><dd>{formatLocalTime(selectedDay.clockInAtLocal, selectedDay.clockInTimeZoneId)}</dd></div>
                  )}
                  {selectedDay.clockOutAtLocal && selectedDay.clockOutTimeZoneId && (
                    <div><dt>Clock out · {selectedDay.clockOutTimeZoneId}</dt><dd>{formatLocalTime(selectedDay.clockOutAtLocal, selectedDay.clockOutTimeZoneId)}</dd></div>
                  )}
                  {isTodaySelected && (
                    <div><dt>Current time · {calendar.timeZoneId}</dt><dd>{getCurrentCompanyClockTime(calendar)}</dd></div>
                  )}
                </dl>
              </>
            )}

            {isTodaySelected && (
                <div className="attendance-punch-actions">
                  <button aria-busy={activeAction === "ClockIn"}
                    className="primary-attendance-button"
                    type="button"
                    disabled={!canClockIn}
                    onClick={() => void handlePunch("ClockIn")}
                  >
                    {activeAction === "ClockIn" ? <LoadingIndicator label="Recording Clock In…" /> : "Clock In now"}
                  </button>
                  <button aria-busy={activeAction === "ClockOut"}
                    className="secondary-attendance-button"
                    type="button"
                    disabled={!canClockOut}
                    onClick={() => void handlePunch("ClockOut")}
                  >
                    {activeAction === "ClockOut" ? <LoadingIndicator label="Recording Clock Out…" /> : "Clock Out now"}
                  </button>
                </div>
            )}

            {selectedDay && (
              <CorrectionRequestForm
                day={selectedDay}
                todayLocalDate={calendar.todayLocalDate}
                timeZoneId={selectedDay.clockInTimeZoneId ?? calendar.timeZoneId}
                isSubmitting={activeAction === "Correction"}
                onSubmit={request => void handleCorrectionSubmit(request)}
              />
            )}
          </section>
        </div>
      </div>
    </ApplicationLayout>
  );
}
