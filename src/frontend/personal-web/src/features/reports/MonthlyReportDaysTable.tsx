import type { MonthlyReportDay } from "./monthlyAttendanceReportApi";
import { formatReportDate, formatReportTime, formatWorkedTime } from "./monthlyAttendanceReportFormatting";

type MonthlyReportDaysTableProps = {
  days: MonthlyReportDay[];
  timeZoneId: string;
};

function getDayStatus(status: MonthlyReportDay["status"]): string {
  switch (status) {
    case "Complete": return "Reported";
    case "OpenShift": return "In progress";
    case "MissingWorkday": return "No report";
    case "TodayNoPunch": return "No report yet today";
    case "UpcomingWorkday": return "Upcoming";
    case "Carryover": return "Continued shift";
    case "PendingApproval": return "Awaiting approval";
    default: return "Scheduled";
  }
}

export default function MonthlyReportDaysTable({ days, timeZoneId }: MonthlyReportDaysTableProps) {
  return (
    <div className="monthly-report-table-wrap" role="region" aria-label="Monthly workday details" tabIndex={0}>
      <table className="monthly-report-table">
        <thead>
          <tr>
            <th scope="col">Date</th>
            <th scope="col">Reporting status</th>
            <th scope="col">Clock In / Clock Out</th>
            <th scope="col">Worked</th>
          </tr>
        </thead>
        <tbody>
          {days.map(day => {
            const workedMinutes = day.shifts.reduce((total, shift) => total + (shift.workedMinutes ?? 0), 0);
            const pending = day.pendingCorrection;
            return (
              <tr key={day.date} className={pending ? "is-pending-approval" : undefined}>
                <td data-label="Date">{formatReportDate(day.date, { weekday: "short", month: "short", day: "numeric", year: "numeric" })}</td>
                <td data-label="Day status">
                  <span className={`monthly-report-day-status is-${day.status.toLowerCase()}`}>
                    {getDayStatus(day.status)}
                  </span>
                </td>
                <td data-label="Clock In / Clock Out">
                  {pending ? (
                    <div className="monthly-report-shift-times">
                      <span className="monthly-report-shift-segment">
                        <strong>Clock In {formatReportTime(pending.clockInAtLocal, pending.timeZoneId)}</strong>
                        <strong>Clock Out {formatReportTime(pending.clockOutAtLocal, pending.timeZoneId)}</strong>
                      </span>
                    </div>
                  ) : day.shifts.length === 0 ? "—" : (
                    <div className="monthly-report-shift-times">
                      {day.shifts.map(shift => (
                        <span className="monthly-report-shift-segment" key={shift.workShiftId}>
                          <strong>
                            {shift.isContinuation
                              ? `Continued from ${formatReportDate(shift.workDate)}`
                              : `Clock In ${formatReportTime(shift.clockInAtLocal, shift.clockInTimeZoneId)}`}
                            {!shift.isContinuation && shift.clockOutAtLocal
                              ? ` · Clock Out ${formatReportTime(shift.clockOutAtLocal, shift.clockOutTimeZoneId ?? timeZoneId)}`
                              : shift.isOpen ? " · In progress" : ""}
                          </strong>
                          <small>
                            {formatReportTime(shift.workedSegmentStartAtLocal, timeZoneId)}
                            {" – "}
                            {shift.workedSegmentEndAtLocal
                              ? formatReportTime(shift.workedSegmentEndAtLocal, timeZoneId)
                              : "Still open"}
                          </small>
                        </span>
                      ))}
                    </div>
                  )}
                </td>
                <td data-label="Worked">{pending
                  ? pending.workedMinutes !== null ? formatWorkedTime(pending.workedMinutes) : "—"
                  : workedMinutes > 0 ? formatWorkedTime(workedMinutes) : "—"}</td>
              </tr>
            );
          })}
        </tbody>
      </table>
    </div>
  );
}
