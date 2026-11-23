import { apiRequest } from "../../lib/apiClient";

export type MonthlyReportShift = {
  workShiftId: string;
  workDate: string;
  clockInAtLocal: string;
  clockInTimeZoneId: string;
  clockOutAtLocal: string | null;
  clockOutTimeZoneId: string | null;
  workedMinutes: number | null;
  isOpen: boolean;
  workedSegmentStartAtLocal: string;
  workedSegmentEndAtLocal: string | null;
  isContinuation: boolean;
};

export type MonthlyReportDay = {
  date: string;
  isWorkday: boolean;
  status: "Complete" | "OpenShift" | "MissingWorkday" | "TodayNoPunch" | "UpcomingWorkday" | "NotScheduled" | "Carryover" | "PendingApproval";
  shifts: MonthlyReportShift[];
  pendingCorrection: {
    clockInAtLocal: string | null;
    clockOutAtLocal: string | null;
    timeZoneId: string;
    workedMinutes: number | null;
  } | null;
};

export type MonthlyAttendanceReport = {
  employeeMembershipId: string;
  employeeName: string;
  employeeNumber: string | null;
  periodStart: string;
  periodEnd: string;
  timeZoneId: string;
  isScheduleConfigured: boolean;
  scheduledWorkdayCount: number;
  completedShiftCount: number;
  openShiftCount: number;
  missingWorkdayCount: number;
  pendingCorrectionCount: number;
  totalWorkedMinutes: number;
  pendingCorrectionMinutes: number;
  days: MonthlyReportDay[];
};

export type MonthlyAttendanceTeamMember = {
  employeeMembershipId: string;
  employeeName: string;
  employeeNumber: string | null;
};

export function getCurrentEmployeeReport(signal?: AbortSignal): Promise<MonthlyAttendanceReport> {
  return apiRequest("/api/monthly-attendance-reports/mine/current", { signal });
}
export function getEmployeeReport(year: number, month: number, signal?: AbortSignal): Promise<MonthlyAttendanceReport> {
  const query = new URLSearchParams({ year: String(year), month: String(month) });
  return apiRequest(`/api/monthly-attendance-reports/mine?${query}`, { signal });
}

export function getMonthlyAttendanceTeam(signal?: AbortSignal): Promise<MonthlyAttendanceTeamMember[]> {
  return apiRequest("/api/monthly-attendance-reports/team/members", { signal });
}

export function getCurrentTeamMemberReport(
  membershipId: string,
  signal?: AbortSignal,
): Promise<MonthlyAttendanceReport> {
  return apiRequest(`/api/monthly-attendance-reports/team/members/${membershipId}/current`, { signal });
}

export function getTeamMemberReport(
  membershipId: string,
  year: number,
  month: number,
  signal?: AbortSignal,
): Promise<MonthlyAttendanceReport> {
  const query = new URLSearchParams({ year: String(year), month: String(month) });
  return apiRequest(`/api/monthly-attendance-reports/team/members/${membershipId}?${query}`, { signal });
}
