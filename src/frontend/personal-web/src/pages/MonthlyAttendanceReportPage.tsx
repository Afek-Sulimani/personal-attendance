import { downloadAttendanceCsv } from "../features/reports/attendanceCsv";
import LoadingIndicator from "../components/LoadingIndicator";
import { useEffect, useState } from "react";
import { useNavigate } from "react-router-dom";
import ApplicationLayout from "../components/ApplicationLayout";
import MonthlyReportDaysTable from "../features/reports/MonthlyReportDaysTable";
import {
  getCurrentEmployeeReport,
  getEmployeeReport,
} from "../features/reports/monthlyAttendanceReportApi";
import type { MonthlyAttendanceReport } from "../features/reports/monthlyAttendanceReportApi";
import { formatReportPeriod, formatWorkedTime, monthKeyToPeriod, payrollMonthKey } from "../features/reports/monthlyAttendanceReportFormatting";
import { getCurrentUser, signOut } from "../features/auth/authApi";
import type { AuthenticatedUser } from "../features/auth/authApi";
import { ApiRequestError } from "../lib/apiClient";

export default function MonthlyAttendanceReportPage() {
  const navigate = useNavigate();
  const [user, setUser] = useState<AuthenticatedUser | null>(null);
  const [report, setReport] = useState<MonthlyAttendanceReport | null>(null);
  const [selectedMonthKey, setSelectedMonthKey] = useState<number | null>(null);
  const [latestMonthKey, setLatestMonthKey] = useState<number | null>(null);
  const [isLoading, setIsLoading] = useState(true);
  const [isSigningOut, setIsSigningOut] = useState(false);
  const [errorMessage, setErrorMessage] = useState("");

  useEffect(() => {
    const controller = new AbortController();
    let isCurrent = true;

    async function loadPage() {
      setIsLoading(true);
      setErrorMessage("");
      try {
        const reportRequest = selectedMonthKey === null
          ? getCurrentEmployeeReport(controller.signal)
          : (() => {
              const month = monthKeyToPeriod(selectedMonthKey);
              return getEmployeeReport(month.year, month.month, controller.signal);
            })();
        const [currentUser, monthlyAttendance] = await Promise.all([
          getCurrentUser(controller.signal),
          reportRequest,
        ]);
        if (!isCurrent) return;
        setUser(currentUser);
        setReport(monthlyAttendance);
        setLatestMonthKey(current => current ?? payrollMonthKey(monthlyAttendance.periodStart));
      } catch (error) {
        if (!isCurrent) return;
        if (error instanceof ApiRequestError && error.status === 401) {
          navigate("/login", { replace: true });
          return;
        }
        setErrorMessage(error instanceof Error ? error.message : "Monthly attendance could not be loaded.");
      } finally {
        if (isCurrent) setIsLoading(false);
      }
    }

    void loadPage();
    return () => {
      isCurrent = false;
      controller.abort();
    };
  }, [navigate, selectedMonthKey]);

  async function handleSignOut() {
    setIsSigningOut(true);
    try {
      await signOut();
      navigate("/login", { replace: true });
    } catch (error) {
      setErrorMessage(error instanceof Error ? error.message : "Sign out could not be completed.");
    } finally {
      setIsSigningOut(false);
    }
  }

  if (isLoading || !user || !report) {
    return <main className="attendance-loading-page"><p role="status">{errorMessage || <LoadingIndicator label="Loading monthly attendance…" />}</p></main>;
  }

  const currentMonthKey = payrollMonthKey(report.periodStart);
  const isCurrentPeriod = currentMonthKey === latestMonthKey;

  return (
    <ApplicationLayout user={user} isSigningOut={isSigningOut} onSignOut={() => void handleSignOut()}>
      <div className="attendance-page-content">
        <header className="attendance-page-heading">
          <div>
            <p className="attendance-eyebrow">EMPLOYEE WORKSPACE</p>
            <h1>My monthly attendance</h1>
            <p className="attendance-page-description">See your daily reported hours and running total for each payroll period.</p>
          </div>
          <span className="attendance-timezone-badge">{report.timeZoneId}</span>
        </header>

        {errorMessage && <div className="status-banner error" role="alert">{errorMessage}</div>}

        <section className="monthly-report-card" aria-labelledby="monthly-attendance-title">
          <button className="secondary-attendance-button" type="button" onClick={() => downloadAttendanceCsv(report)}>Download CSV</button>
          <div className="monthly-report-heading">
            <div>
              <p className="attendance-eyebrow">PAYROLL PERIOD</p>
              <h2 id="monthly-attendance-title">{formatReportPeriod(report.periodStart, report.periodEnd)}</h2>
            </div>
            <div className="monthly-report-period-controls" aria-label="Choose a payroll period">
              <button
                className="calendar-nav-button"
                type="button"
                aria-label="Previous payroll period"
                onClick={() => setSelectedMonthKey(currentMonthKey - 1)}
              >‹</button>
              <button
                className="calendar-nav-button"
                type="button"
                aria-label="Next payroll period"
                onClick={() => setSelectedMonthKey(currentMonthKey + 1)}
                disabled={isCurrentPeriod || latestMonthKey === null || currentMonthKey >= latestMonthKey}
              >›</button>
            </div>
          </div>

          {!report.isScheduleConfigured && (
            <div className="monthly-report-notice" role="status">
              The company work schedule has not been configured yet.
            </div>
          )}

          <div className="monthly-report-summary" aria-label="Monthly attendance totals">
            <article><span>{isCurrentPeriod ? "Total reported so far (including pending)" : "Total reported (including pending)"}</span><strong>{formatWorkedTime(report.totalWorkedMinutes)}</strong></article>
            <article><span>Awaiting approval</span><strong>{formatWorkedTime(report.pendingCorrectionMinutes)}</strong></article>
            <article><span>Reported workdays</span><strong>{report.completedShiftCount}</strong></article>
            <article><span>Open shifts</span><strong>{report.openShiftCount}</strong></article>
            <article><span>Past workdays without a report</span><strong>{report.missingWorkdayCount}</strong></article>
            <article>
              {report.pendingCorrectionCount > 0 ? (
                <button
                  className="monthly-report-pending-jump"
                  type="button"
                  onClick={() => document.querySelector(".monthly-report-table tr.is-pending-approval")?.scrollIntoView({ behavior: "smooth", block: "center" })}
                  aria-label={`Show ${report.pendingCorrectionCount} pending corrections in the table`}
                >
                  <span>Pending corrections</span><strong>{report.pendingCorrectionCount} ↓</strong>
                </button>
              ) : <><span>Pending corrections</span><strong>0</strong></>}
            </article>
          </div>

          {report.days.length > 0 ? (
            <MonthlyReportDaysTable days={report.days} timeZoneId={report.timeZoneId} />
          ) : (
            <div className="correction-empty-state" role="status">
              <h2>No workdays are configured for this payroll period</h2>
              <p>Only dates included in the employee's work schedule appear in this table.</p>
            </div>
          )}
        </section>
      </div>
    </ApplicationLayout>
  );
}
