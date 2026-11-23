import type { MonthlyAttendanceReport } from "./monthlyAttendanceReportApi";

// Quote every field and neutralize spreadsheet formula prefixes in user-supplied values.
function field(value: unknown): string {
  let text = value == null ? "" : String(value);
  if (/^[\s]*[=+@-]/.test(text)) text = `'${text}`;
  return `"${text.replaceAll('"', '""')}"`;
}

export function attendanceCsv(report: MonthlyAttendanceReport): string {
  const rows: unknown[][] = [["Employee", "Employee number", "Date", "Status", "Clock In", "Clock Out", "Time zone", "Minutes", "Pending approval"]];
  for (const day of report.days) {
    if (day.pendingCorrection) {
      const pending = day.pendingCorrection;
      rows.push([report.employeeName, report.employeeNumber, day.date, "Awaiting approval", pending.clockInAtLocal, pending.clockOutAtLocal, pending.timeZoneId, pending.workedMinutes, "Yes"]);
    } else if (day.shifts.length) {
      for (const shift of day.shifts) rows.push([report.employeeName, report.employeeNumber, day.date, day.status, shift.clockInAtLocal, shift.clockOutAtLocal, shift.clockInTimeZoneId, shift.workedMinutes, "No"]);
    } else rows.push([report.employeeName, report.employeeNumber, day.date, day.status, "", "", report.timeZoneId, "", "No"]);
  }
  rows.push(["Total including pending", "", report.periodStart, report.periodEnd, "", "", report.timeZoneId, report.totalWorkedMinutes, ""]);
  rows.push(["Pending subtotal", "", "", "", "", "", report.timeZoneId, report.pendingCorrectionMinutes, "Yes"]);
  return "\uFEFF" + rows.map(row => row.map(field).join(",")).join("\r\n");
}

export function downloadAttendanceCsv(report: MonthlyAttendanceReport) {
  const url = URL.createObjectURL(new Blob([attendanceCsv(report)], {type: "text/csv;charset=utf-8"}));
  const anchor = document.createElement("a");
  anchor.href = url;
  anchor.download = `attendance-${report.periodStart}-${report.periodEnd}.csv`;
  anchor.click();
  setTimeout(() => URL.revokeObjectURL(url), 1000);
}
