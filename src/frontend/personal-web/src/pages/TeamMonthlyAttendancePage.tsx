import { downloadAttendanceCsv } from "../features/reports/attendanceCsv";
import LoadingIndicator from "../components/LoadingIndicator";
import { useEffect, useState } from "react";
import { useNavigate } from "react-router-dom";
import ApplicationLayout from "../components/ApplicationLayout";
import MonthlyReportDaysTable from "../features/reports/MonthlyReportDaysTable";
import {
  getCurrentTeamMemberReport,
  getMonthlyAttendanceTeam,
  getTeamMemberReport,
} from "../features/reports/monthlyAttendanceReportApi";
import type {
  MonthlyAttendanceReport,
  MonthlyAttendanceTeamMember,
} from "../features/reports/monthlyAttendanceReportApi";
import { formatReportPeriod, formatWorkedTime, monthKeyToPeriod, payrollMonthKey } from "../features/reports/monthlyAttendanceReportFormatting";
import { getCurrentUser, signOut } from "../features/auth/authApi";
import type { AuthenticatedUser } from "../features/auth/authApi";
import { ApiRequestError } from "../lib/apiClient";

export default function TeamMonthlyAttendancePage() {
  const navigate = useNavigate();
  const [user, setUser] = useState<AuthenticatedUser | null>(null);
  const [team, setTeam] = useState<MonthlyAttendanceTeamMember[]>([]);
  const [selectedMembershipId, setSelectedMembershipId] = useState("");
  const [report, setReport] = useState<MonthlyAttendanceReport | null>(null);
  const [selectedMonthKey, setSelectedMonthKey] = useState<number | null>(null);
  const [latestMonthKey, setLatestMonthKey] = useState<number | null>(null);
  const [isLoading, setIsLoading] = useState(true);
  const [isReportLoading, setIsReportLoading] = useState(false);
  const [isSigningOut, setIsSigningOut] = useState(false);
  const [errorMessage, setErrorMessage] = useState("");

  useEffect(() => {
    const controller = new AbortController();
    let isCurrent = true;

    async function loadPage() {
      setIsLoading(true);
      setErrorMessage("");
      try {
        const [currentUser, members] = await Promise.all([
          getCurrentUser(controller.signal),
          getMonthlyAttendanceTeam(controller.signal),
        ]);
        if (!isCurrent) return;
        setUser(currentUser);
        setTeam(members);
        setSelectedMembershipId(current =>
          current && members.some(member => member.employeeMembershipId === current)
            ? current
            : members[0]?.employeeMembershipId ?? "",
        );
      } catch (error) {
        if (!isCurrent) return;
        if (error instanceof ApiRequestError && error.status === 401) {
          navigate("/login", { replace: true });
          return;
        }
        setErrorMessage(error instanceof Error ? error.message : "Team attendance could not be loaded.");
      } finally {
        if (isCurrent) setIsLoading(false);
      }
    }

    void loadPage();
    return () => {
      isCurrent = false;
      controller.abort();
    };
  }, [navigate]);

  useEffect(() => {
    if (!selectedMembershipId) {
      setReport(null);
      return;
    }
    const controller = new AbortController();
    let isCurrent = true;

    async function loadReport() {
      setIsReportLoading(true);
      setErrorMessage("");
      try {
        const nextReport = selectedMonthKey === null
          ? await getCurrentTeamMemberReport(selectedMembershipId, controller.signal)
          : await (() => {
              const month = monthKeyToPeriod(selectedMonthKey);
              return getTeamMemberReport(selectedMembershipId, month.year, month.month, controller.signal);
            })();
        if (!isCurrent) return;
        setReport(nextReport);
        setLatestMonthKey(current => current ?? payrollMonthKey(nextReport.periodStart));
      } catch (error) {
        if (!isCurrent) return;
        if (error instanceof ApiRequestError && error.status === 401) {
          navigate("/login", { replace: true });
          return;
        }
        setErrorMessage(error instanceof Error ? error.message : "The employee attendance could not be loaded.");
      } finally {
        if (isCurrent) setIsReportLoading(false);
      }
    }

    void loadReport();
    return () => {
      isCurrent = false;
      controller.abort();
    };
  }, [navigate, selectedMembershipId, selectedMonthKey]);

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

  if (isLoading || !user) {
    return <main className="attendance-loading-page"><p role="status">{errorMessage || <LoadingIndicator label="Loading team attendance…" />}</p></main>;
  }

  const currentMonthKey = report ? payrollMonthKey(report.periodStart) : null;
  const isCurrentPeriod = currentMonthKey !== null && currentMonthKey === latestMonthKey;

  return (
    <ApplicationLayout user={user} isSigningOut={isSigningOut} onSignOut={() => void handleSignOut()}>
      <div className="attendance-page-content">
        <header className="attendance-page-heading">
          <div>
            <p className="attendance-eyebrow">MANAGER WORKSPACE</p>
            <h1>Monthly attendance</h1>
            <p className="attendance-page-description">Review reported workdays, daily hours, and totals for each payroll period.</p>
          </div>
          {report && <span className="attendance-timezone-badge">{report.timeZoneId}</span>}
        </header>

        {errorMessage && <div className="status-banner error" role="alert">{errorMessage}</div>}

        {team.length === 0 ? (
          <section className="correction-empty-state" role="status">
            <h2>No employees are available to review</h2>
            <p>Employees assigned to you or available under your company role will appear here.</p>
          </section>
        ) : (
          <section className="monthly-report-card" aria-labelledby="team-attendance-title">
            <div className="monthly-review-toolbar">
              <label className="payroll-employee-select">
                <span>Employee</span>
                <select value={selectedMembershipId} onChange={event => {
                  setSelectedMembershipId(event.target.value);
                  setSelectedMonthKey(null);
                  setLatestMonthKey(null);
                  setReport(null);
                }}>
                  {team.map(member => (
                    <option key={member.employeeMembershipId} value={member.employeeMembershipId}>
                      {member.employeeName}{member.employeeNumber ? ` · ${member.employeeNumber}` : ""}
                    </option>
                  ))}
                </select>
              </label>
              <span className="monthly-review-employee-count">{team.length} employee{team.length === 1 ? "" : "s"}</span>
            </div>

            {isReportLoading || !report ? (
              <p className="monthly-review-loading" role="status">{errorMessage || <LoadingIndicator label="Loading the selected employee…" />}</p>
            ) : (
              <>
                <button className="secondary-attendance-button" type="button" onClick={() => downloadAttendanceCsv(report)}>Download CSV</button>
          <div className="monthly-report-heading">
                  <div>
                    <p className="attendance-eyebrow">PAYROLL PERIOD</p>
                    <h2 id="team-attendance-title">{formatReportPeriod(report.periodStart, report.periodEnd)}</h2>
                  </div>
                  <div className="monthly-report-period-controls" aria-label="Choose a payroll period">
                    <button
                      className="calendar-nav-button"
                      type="button"
                      aria-label="Previous payroll period"
                      onClick={() => setSelectedMonthKey((currentMonthKey ?? 0) - 1)}
                    >‹</button>
                    <button
                      className="calendar-nav-button"
                      type="button"
                      aria-label="Next payroll period"
                      onClick={() => setSelectedMonthKey((currentMonthKey ?? 0) + 1)}
                      disabled={isCurrentPeriod || latestMonthKey === null || currentMonthKey === null || currentMonthKey >= latestMonthKey}
                    >›</button>
                  </div>
                </div>

                {!report.isScheduleConfigured && (
                  <div className="monthly-report-notice" role="status">
                    The company work schedule has not been configured yet.
                  </div>
                )}

                <div className="monthly-report-summary" aria-label="Employee monthly attendance totals">
                  <article><span>{isCurrentPeriod ? "Total reported so far (including pending)" : "Total reported (including pending)"}</span><strong>{formatWorkedTime(report.totalWorkedMinutes)}</strong></article>
                  <article><span>Awaiting approval</span><strong>{formatWorkedTime(report.pendingCorrectionMinutes)}</strong></article>
                  <article><span>Reported workdays</span><strong>{report.completedShiftCount}</strong></article>
                  <article><span>Open shifts</span><strong>{report.openShiftCount}</strong></article>
                  <article><span>Past workdays without a report</span><strong>{report.missingWorkdayCount}</strong></article>
                  <article><span>Pending corrections</span><strong>{report.pendingCorrectionCount}</strong></article>
                </div>

                {report.days.length > 0 ? (
                  <MonthlyReportDaysTable days={report.days} timeZoneId={report.timeZoneId} />
                ) : (
                  <div className="correction-empty-state" role="status">
                    <h2>No workdays are configured for this payroll period</h2>
                    <p>Only dates included in this employee's work schedule appear in the table.</p>
                  </div>
                )}
              </>
            )}
          </section>
        )}
      </div>
    </ApplicationLayout>
  );
}
