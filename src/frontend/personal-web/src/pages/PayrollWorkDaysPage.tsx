import LoadingIndicator from "../components/LoadingIndicator";
import { useCallback, useEffect, useState } from "react";
import { useNavigate } from "react-router-dom";
import ApplicationLayout from "../components/ApplicationLayout";
import { getMonthlyAttendanceCalendar } from "../features/attendance/attendanceApi";
import WorkDaysCalendar from "../features/payroll/WorkDaysCalendar";
import {
  getMonthlyWorkSchedule,
  getPayrollEmployees,
  saveMonthlyWorkSchedule,
} from "../features/payroll/payrollApi";
import type { PayrollEmployee } from "../features/payroll/payrollApi";
import { getCurrentUser, signOut } from "../features/auth/authApi";
import type { AuthenticatedUser } from "../features/auth/authApi";
import { ApiRequestError } from "../lib/apiClient";
import { matchesEmployeeSearch } from "../lib/matchesEmployeeSearch";

function formatMonth(year: number, month: number): string {
  return new Intl.DateTimeFormat("en-US", {
    month: "long",
    year: "numeric",
    timeZone: "UTC",
  }).format(new Date(Date.UTC(year, month - 1, 15)));
}

function moveMonth(year: number, month: number, direction: -1 | 1) {
  const target = new Date(Date.UTC(year, month - 1 + direction, 1));
  return { year: target.getUTCFullYear(), month: target.getUTCMonth() + 1 };
}

const weekdays = ["Sunday", "Monday", "Tuesday", "Wednesday", "Thursday", "Friday", "Saturday"];

function getDatesForWeekdays(year: number, month: number, days: ReadonlySet<number>): string[] {
  return Array.from({ length: new Date(Date.UTC(year, month, 0)).getUTCDate() }, (_, index) => index + 1)
    .filter(day => days.has(new Date(Date.UTC(year, month - 1, day)).getUTCDay()))
    .map(day => `${year}-${String(month).padStart(2, "0")}-${String(day).padStart(2, "0")}`);
}

function getFullWeekdays(year: number, month: number, dates: ReadonlySet<string>): Set<number> {
  return new Set(weekdays.map((_, day) => day).filter(day =>
    getDatesForWeekdays(year, month, new Set([day])).every(date => dates.has(date))
  ));
}

export default function PayrollWorkDaysPage() {
  const navigate = useNavigate();
  const [user, setUser] = useState<AuthenticatedUser | null>(null);
  const [employees, setEmployees] = useState<PayrollEmployee[]>([]);
  const [employeeMembershipId, setEmployeeMembershipId] = useState("");
  const [employeeSearch, setEmployeeSearch] = useState("");
  const [year, setYear] = useState<number | null>(null);
  const [month, setMonth] = useState<number | null>(null);
  const [todayLocalDate, setTodayLocalDate] = useState("");
  const [selectedWeekdays, setSelectedWeekdays] = useState<Set<number>>(new Set());
  const [isScheduleConfigured, setIsScheduleConfigured] = useState(false);
  const [isLoading, setIsLoading] = useState(true);
  const [isSaving, setIsSaving] = useState(false);
  const [isScheduleLoading, setIsScheduleLoading] = useState(false);
  const [isScheduleLoadFailed, setIsScheduleLoadFailed] = useState(false);
  const [isSigningOut, setIsSigningOut] = useState(false);
  const [errorMessage, setErrorMessage] = useState("");
  const [statusMessage, setStatusMessage] = useState("");

  const handleError = useCallback((error: unknown) => {
    if (error instanceof ApiRequestError && error.status === 401) {
      navigate("/login", { replace: true });
      return;
    }
    setErrorMessage(error instanceof Error ? error.message : "Payroll data could not be loaded.");
  }, [navigate]);

  useEffect(() => {
    const controller = new AbortController();
    let isCurrent = true;

    async function loadPage() {
      try {
        const [currentUser, payrollEmployees, calendar] = await Promise.all([
          getCurrentUser(controller.signal),
          getPayrollEmployees(controller.signal),
          getMonthlyAttendanceCalendar(undefined, undefined, controller.signal),
        ]);
        if (!isCurrent) return;
        setUser(currentUser);
        setEmployees(payrollEmployees);
        setEmployeeMembershipId(payrollEmployees[0]?.membershipId ?? "");
        setYear(calendar.year);
        setMonth(calendar.month);
        setTodayLocalDate(calendar.todayLocalDate);
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

  useEffect(() => {
    if (!employeeMembershipId || year === null || month === null) return;
    const controller = new AbortController();
    let isCurrent = true;
    setIsScheduleLoading(true);
    setIsScheduleLoadFailed(false);
    setErrorMessage("");

    getMonthlyWorkSchedule(employeeMembershipId, year, month, controller.signal)
      .then(schedule => {
        if (!isCurrent) return;
        setSelectedWeekdays(getFullWeekdays(year, month, new Set(schedule.workingDates)));
        setIsScheduleConfigured(schedule.isConfigured);
        setIsScheduleLoadFailed(false);
      })
      .catch(error => {
        if (isCurrent) {
          setSelectedWeekdays(new Set());
          setIsScheduleLoadFailed(true);
          handleError(error);
        }
      })
      .finally(() => {
        if (isCurrent) setIsScheduleLoading(false);
      });

    return () => {
      isCurrent = false;
      controller.abort();
    };
  }, [employeeMembershipId, year, month, handleError]);

  function toggleWeekday(day: number) {
    setSelectedWeekdays(current => {
      const updated = new Set(current);
      if (updated.has(day)) updated.delete(day);
      else updated.add(day);
      return updated;
    });
    setStatusMessage("");
  }

  async function handleSave() {
    if (year === null || month === null || !employeeMembershipId) return;
    setIsSaving(true);
    setErrorMessage("");
    setStatusMessage("");
    try {
      const saved = await saveMonthlyWorkSchedule(
        employeeMembershipId,
        year,
        month,
        getDatesForWeekdays(year, month, selectedWeekdays),
      );
      setSelectedWeekdays(getFullWeekdays(year, month, new Set(saved.workingDates)));
      setIsScheduleConfigured(saved.isConfigured);
      setStatusMessage(`Workdays saved for ${formatMonth(year, month)}.`);
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

  if (isLoading || !user || year === null || month === null) {
    return <main className="attendance-loading-page"><p role="status">{errorMessage || <LoadingIndicator label="Loading payroll workdays…" />}</p></main>;
  }

  const selectedEmployee = employees.find(employee => employee.membershipId === employeeMembershipId);
  const filteredEmployees = employees.filter(employee =>
    matchesEmployeeSearch(employee.displayName, employee.employeeNumber, employeeSearch)
  );
  const previewDates = new Set(getDatesForWeekdays(year, month, selectedWeekdays));

  return (
    <ApplicationLayout user={user} isSigningOut={isSigningOut} onSignOut={() => void handleSignOut()}>
      <div className="attendance-page-content">
        <header className="attendance-page-heading">
          <div>
            <p className="attendance-eyebrow">PAYROLL</p>
            <h1>Workday setup</h1>
            <p className="attendance-page-description">Choose the weekdays this employee can work. The calendar previews the selected month.</p>
          </div>
          <span className="attendance-timezone-badge">Calendar month</span>
        </header>

        {errorMessage && <div className="status-banner error" role="alert">{errorMessage}</div>}
        {statusMessage && <div className="status-banner success" role="status">{statusMessage}</div>}

        <section className="payroll-workday-card" aria-label="Configure employee workdays">
          <label className="form-field payroll-employee-select">
            <span>Search employees</span>
            <input type="search" value={employeeSearch} onChange={event => setEmployeeSearch(event.target.value)} placeholder="Name or employee number (1 matches 00001)" />
          </label>
          <label className="form-field payroll-employee-select">
            <span>Employee</span>
            <select
              value={employeeMembershipId}
              onChange={event => setEmployeeMembershipId(event.target.value)}
              disabled={employees.length === 0 || isScheduleLoading}
            >
              {!filteredEmployees.some(employee => employee.membershipId === employeeMembershipId) &&
                <option value={employeeMembershipId} hidden>{selectedEmployee?.displayName ?? "Select an employee"}</option>}
              {filteredEmployees.map(employee => (
                <option value={employee.membershipId} key={employee.membershipId}>
                  {employee.displayName}{employee.employeeNumber ? ` · ${employee.employeeNumber}` : ""}
                </option>
              ))}
            </select>
          </label>
          {filteredEmployees.length === 0 && employees.length > 0 && <p className="field-hint">No employees match this search.</p>}

          <div className="workday-month-header">
            <button
              className="calendar-nav-button"
              type="button"
              aria-label="Previous month"
              disabled={isScheduleLoading}
              onClick={() => {
                const target = moveMonth(year, month, -1);
                setYear(target.year);
                setMonth(target.month);
              }}
            >
              ‹
            </button>
            <h2>{formatMonth(year, month)}</h2>
            <button
              className="calendar-nav-button"
              type="button"
              aria-label="Next month"
              disabled={isScheduleLoading}
              onClick={() => {
                const target = moveMonth(year, month, 1);
                setYear(target.year);
                setMonth(target.month);
              }}
            >
              ›
            </button>
          </div>

          {employees.length === 0 ? (
            <div className="status-banner information" role="status">
              No active employee profiles are available in this company yet.
            </div>
          ) : selectedEmployee ? (
            <>
              {!isScheduleConfigured && (
                <div className="status-banner information" role="status">
                  No schedule is saved for {selectedEmployee.displayName} for this month. Choose weekdays and save to configure it.
                </div>
              )}
              {isScheduleLoading ? (
                <p className="session-check" role="status"><LoadingIndicator label="Loading this employee’s schedule…" /></p>
              ) : (
                <>
                  <fieldset className="company-weekdays-fieldset">
                    <legend>Working weekdays</legend>
                    <p>Choosing a weekday includes every occurrence of that day in the selected month.</p>
                    <div className="company-weekdays-grid">
                      {weekdays.map((day, index) => (
                        <label className="company-weekday-option" key={day}>
                          <input type="checkbox" checked={selectedWeekdays.has(index)} onChange={() => toggleWeekday(index)} />
                          <span>{day}</span>
                        </label>
                      ))}
                    </div>
                  </fieldset>
                  <WorkDaysCalendar year={year} month={month} todayLocalDate={todayLocalDate} workingDates={previewDates} />
                </>
              )}
              <div className="workday-selection-summary">
                <span>{previewDates.size} scheduled workdays</span>
                <span>Today in the company time zone is outlined</span>
              </div>
              <button aria-busy={isSaving}
                className="primary-attendance-button"
                type="button"
                onClick={() => void handleSave()}
                disabled={isSaving || isScheduleLoading || isScheduleLoadFailed || employees.length === 0}
              >
                {isSaving ? <LoadingIndicator label="Saving workdays…" /> : "Save workdays"}
              </button>
            </>
          ) : null}
        </section>
      </div>
    </ApplicationLayout>
  );
}
