import LoadingIndicator from "../components/LoadingIndicator";
import { useCallback, useEffect, useState } from "react";
import { useNavigate } from "react-router-dom";
import ApplicationLayout from "../components/ApplicationLayout";
import AdminAttendanceShiftEditor from "../features/attendance/AdminAttendanceShiftEditor";
import {
  getAdminAttendanceMonth,
  updateAdminAttendanceShift,
} from "../features/attendance/adminAttendanceApi";
import type {
  AdminAttendanceChange,
  AdminAttendanceMonth,
} from "../features/attendance/adminAttendanceApi";
import { getMonthlyAttendanceCalendar } from "../features/attendance/attendanceApi";
import { getCurrentUser, signOut } from "../features/auth/authApi";
import type { AuthenticatedUser } from "../features/auth/authApi";
import { getPayrollEmployees } from "../features/payroll/payrollApi";
import type { PayrollEmployee } from "../features/payroll/payrollApi";
import { ApiRequestError } from "../lib/apiClient";

function moveMonth(year: number, month: number, direction: -1 | 1) {
  const target = new Date(Date.UTC(year, month - 1 + direction, 1));
  return { year: target.getUTCFullYear(), month: target.getUTCMonth() + 1 };
}

function formatMonth(year: number, month: number) {
  return new Intl.DateTimeFormat("en-US", {
    month: "long",
    year: "numeric",
    timeZone: "UTC",
  }).format(new Date(Date.UTC(year, month - 1, 15)));
}

function formatLocalTime(value: string | null, timeZoneId: string | null) {
  if (!value || !timeZoneId) return "Not recorded";
  return new Intl.DateTimeFormat("en-GB", {
    dateStyle: "medium",
    timeStyle: "short",
    timeZone: timeZoneId,
  }).format(new Date(value));
}

function formatSource(source: string | null, provider: string | null, sampleUtc: string | null) {
  const providerDetails = provider ? ` · ${provider}` : "";
  const sampleDetails = sampleUtc ? ` · sample ${new Date(sampleUtc).toISOString()}` : "";
  return `${source ?? "Not recorded"}${providerDetails}${sampleDetails}`;
}

function AuditCard({ change }: { change: AdminAttendanceChange }) {
  return (
    <article className="admin-audit-card">
      <div className="admin-audit-heading">
        <div>
          <strong>{change.employeeName} · {change.workDate}</strong>
          {change.previousWorkDate !== change.workDate && (
            <span>Work date changed from {change.previousWorkDate} to {change.workDate}</span>
          )}
        </div>
        <span>Changed by {change.changedByName} at {formatLocalTime(change.changedAtLocal, change.changeTimeZoneId)}</span>
      </div>
      <dl className="admin-audit-values">
        <div><dt>Clock In before</dt><dd>{formatLocalTime(change.previousClockInAtLocal, change.previousClockInTimeZoneId)}</dd></div>
        <div><dt>Clock In after</dt><dd>{formatLocalTime(change.clockInAtLocal, change.clockInTimeZoneId)}</dd></div>
        <div><dt>Clock Out before</dt><dd>{formatLocalTime(change.previousClockOutAtLocal, change.previousClockOutTimeZoneId)}</dd></div>
        <div><dt>Clock Out after</dt><dd>{formatLocalTime(change.clockOutAtLocal, change.clockOutTimeZoneId)}</dd></div>
      </dl>
      <p className="admin-audit-reason"><strong>Reason:</strong> {change.reason}</p>
      <div className="admin-audit-provenance">
        <p>Clock In source: {formatSource(change.previousClockInSource, change.previousClockInProvider, change.previousClockInProviderSampleUtc)} → {formatSource(change.clockInSource, change.clockInProvider, change.clockInProviderSampleUtc)}</p>
        <p>Clock Out source: {formatSource(change.previousClockOutSource, change.previousClockOutProvider, change.previousClockOutProviderSampleUtc)} → {formatSource(change.clockOutSource, change.clockOutProvider, change.clockOutProviderSampleUtc)}</p>
        <p>Audit timestamp source: {change.changeTimeProvider}; sample: {new Date(change.changeProviderSampleUtc).toISOString()}</p>
      </div>
    </article>
  );
}

export default function AdminAttendancePage() {
  const navigate = useNavigate();
  const [user, setUser] = useState<AuthenticatedUser | null>(null);
  const [employees, setEmployees] = useState<PayrollEmployee[]>([]);
  const [employeeMembershipId, setEmployeeMembershipId] = useState("");
  const [year, setYear] = useState<number | null>(null);
  const [month, setMonth] = useState<number | null>(null);
  const [attendanceMonth, setAttendanceMonth] = useState<AdminAttendanceMonth | null>(null);
  const [activeShiftId, setActiveShiftId] = useState<string | null>(null);
  const [isLoading, setIsLoading] = useState(true);
  const [isMonthLoading, setIsMonthLoading] = useState(false);
  const [isSigningOut, setIsSigningOut] = useState(false);
  const [errorMessage, setErrorMessage] = useState("");
  const [statusMessage, setStatusMessage] = useState("");

  const handleError = useCallback((error: unknown) => {
    if (error instanceof ApiRequestError && error.status === 401) {
      navigate("/login", { replace: true });
      return;
    }
    if (error instanceof ApiRequestError && error.status === 403) {
      setErrorMessage("You do not have access to attendance administration.");
      return;
    }
    setErrorMessage(error instanceof Error ? error.message : "Admin attendance could not be loaded.");
  }, [navigate]);

  useEffect(() => {
    const controller = new AbortController();
    let isCurrent = true;

    async function loadPage() {
      try {
        const [currentUser, companyEmployees, calendar] = await Promise.all([
          getCurrentUser(controller.signal),
          getPayrollEmployees(controller.signal),
          getMonthlyAttendanceCalendar(undefined, undefined, controller.signal),
        ]);
        if (!isCurrent) return;
        setUser(currentUser);
        setEmployees(companyEmployees);
        setEmployeeMembershipId(currentUser.membershipId);
        setYear(calendar.year);
        setMonth(calendar.month);
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

  const loadMonth = useCallback(async (signal?: AbortSignal) => {
    if (!employeeMembershipId || year === null || month === null) return;
    setIsMonthLoading(true);
    setErrorMessage("");
    try {
      const data = await getAdminAttendanceMonth(employeeMembershipId, year, month, signal);
      setAttendanceMonth(data);
    } catch (error) {
      if (!signal?.aborted) handleError(error);
    } finally {
      if (!signal?.aborted) setIsMonthLoading(false);
    }
  }, [employeeMembershipId, year, month, handleError]);

  useEffect(() => {
    if (!employeeMembershipId || year === null || month === null) return;
    const controller = new AbortController();
    void loadMonth(controller.signal);
    return () => controller.abort();
  }, [employeeMembershipId, year, month, loadMonth]);

  async function handleSaveShift(
    shiftId: string,
    input: { clockInLocalDateTime: string; clockOutLocalDateTime?: string; reason: string },
  ) {
    setActiveShiftId(shiftId);
    setErrorMessage("");
    setStatusMessage("");
    try {
      await updateAdminAttendanceShift(shiftId, input);
      setStatusMessage("Attendance was updated and the before/after values were recorded in the audit log.");
      await loadMonth();
    } catch (error) {
      handleError(error);
    } finally {
      setActiveShiftId(null);
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

  if (isLoading || !user || year === null || month === null) {
    return <main className="attendance-loading-page"><p role="status">{errorMessage || <LoadingIndicator label="Loading Admin attendance…" />}</p></main>;
  }

  const selectedEmployee = employees.find(employee => employee.membershipId === employeeMembershipId);

  return (
    <ApplicationLayout user={user} isSigningOut={isSigningOut} onSignOut={() => void handleSignOut()}>
      <div className="attendance-page-content">
        <header className="attendance-page-heading">
          <div>
            <p className="attendance-eyebrow">ADMINISTRATION</p>
            <h1>Attendance records</h1>
            <p className="attendance-page-description">Correct existing shifts with a required reason and a permanent audit record.</p>
          </div>
        </header>

        {errorMessage && <div className="status-banner error" role="alert">{errorMessage}</div>}
        {statusMessage && <div className="status-banner success" role="status">{statusMessage}</div>}

        <section className="payroll-workday-card admin-attendance-browser" aria-label="Choose employee and payroll month">
          <label className="form-field">
            <span>Employee</span>
            <select value={employeeMembershipId} onChange={event => setEmployeeMembershipId(event.target.value)}>
              {employees.map(employee => (
                <option key={employee.membershipId} value={employee.membershipId}>
                  {employee.displayName}{employee.employeeNumber ? ` · ${employee.employeeNumber}` : ""}
                </option>
              ))}
            </select>
          </label>
          <div className="workday-month-header">
            <button className="calendar-nav-button" type="button" aria-label="Previous month" disabled={isMonthLoading}
              onClick={() => { const next = moveMonth(year, month, -1); setYear(next.year); setMonth(next.month); }}>‹</button>
            <h2>{formatMonth(year, month)}</h2>
            <button className="calendar-nav-button" type="button" aria-label="Next month" disabled={isMonthLoading}
              onClick={() => { const next = moveMonth(year, month, 1); setYear(next.year); setMonth(next.month); }}>›</button>
          </div>
        </section>

        {!selectedEmployee || employees.length === 0 ? (
          <section className="correction-empty-state"><h2>No employee profiles are available</h2><p>Add a company member before reviewing attendance records.</p></section>
        ) : isMonthLoading || !attendanceMonth ? (
          <p className="session-check" role="status"><LoadingIndicator label={`Loading attendance for ${selectedEmployee.displayName}…`} /></p>
        ) : (
          <>
            <section className="admin-attendance-section">
              <div className="admin-section-heading">
                <div><p className="attendance-eyebrow">EXISTING SHIFTS</p><h2>{selectedEmployee.displayName}</h2></div>
                <span>{attendanceMonth.shifts.length} shifts</span>
              </div>
              {attendanceMonth.shifts.length === 0 ? (
                <div className="status-banner information" role="status">
                  No shifts exist for this employee in this month. Admin can edit an existing shift here; this page does not create shifts.
                </div>
              ) : (
                <div className="admin-attendance-shift-list">
                  {attendanceMonth.shifts.map(shift => (
                    <AdminAttendanceShiftEditor
                      key={shift.workShiftId}
                      shift={shift}
                      isSaving={activeShiftId === shift.workShiftId}
                      onSave={input => void handleSaveShift(shift.workShiftId, input)}
                    />
                  ))}
                </div>
              )}
            </section>

            <section className="admin-attendance-section">
              <div className="admin-section-heading">
                <div><p className="attendance-eyebrow">AUDIT HISTORY</p><h2>Admin changes</h2></div>
                <span>{attendanceMonth.changes.length} changes</span>
              </div>
              {attendanceMonth.changes.length === 0 ? (
                <div className="correction-empty-state"><h2>No Admin changes recorded</h2><p>Every saved edit will show its reason and before/after values here.</p></div>
              ) : (
                <div className="admin-audit-list">
                  {attendanceMonth.changes.map(change => <AuditCard change={change} key={change.auditId} />)}
                </div>
              )}
            </section>
          </>
        )}
      </div>
    </ApplicationLayout>
  );
}
