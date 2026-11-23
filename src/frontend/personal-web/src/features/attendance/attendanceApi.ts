import { apiRequest } from "../../lib/apiClient";

// Mirror the API's calendar states so React can render server-authoritative attendance status.
export type AttendanceDayStatus =
  | "Complete"
  | "Open"
  | "Missing"
  | "Future"
  | "NonWorking"
  | "NoPunchToday"
  | "CorrectionPending"
  | "CorrectionRejected"
  | "ScheduleNotConfigured";

// Define one date returned by the calendar endpoint, including the zone saved with each historical punch.
export type AttendanceCalendarDay = {
  date: string;
  isWorkDay: boolean | null;
  status: AttendanceDayStatus;
  clockInTimeZoneId: string | null;
  clockInAtLocal: string | null;
  clockOutTimeZoneId: string | null;
  clockOutAtLocal: string | null;
  correctionStatus: "PendingApproval" | "Approved" | "Rejected" | null;
};

// Define the selected month and company-clock information returned by the backend.
export type AttendanceCalendar = {
  year: number;
  month: number;
  timeZoneId: string;
  todayLocalDate: string;
  currentPayrollPeriodStartDate: string | null;
  currentPayrollPeriodEndDate: string | null;
  currentLocalTime: string;
  scheduleConfigured: boolean;
  days: AttendanceCalendarDay[];
};

// Describe the open shift currently eligible for a Clock Out action.
export type OpenShiftDetails = {
  workShiftId: string;
  clockInAtUtc: string;
  clockInTimeZoneId: string;
  clockInAtLocal: string;
  clockInProvider: string | null;
  clockInProviderSampleUtc: string | null;
};

// Keep the open-shift flag and its details together so the UI cannot infer state from local time.
export type AttendanceState = {
  hasOpenShift: boolean;
  openShift: OpenShiftDetails | null;
};

// Carry the backend's saved UTC time, displayed local time, and provider provenance back to the UI.
export type AttendancePunchResult = {
  workShiftId: string;
  status: "ClockedIn" | "ClockedOut";
  occurredAtUtc: string;
  occurredAtLocal: string;
  timeZone: string;
  provider: string;
  providerSampleUtc: string;
};

// Load current open-shift state; this GET does not create or modify attendance data.
export function getCurrentAttendance(signal?: AbortSignal): Promise<AttendanceState> {
  return apiRequest<AttendanceState>("/api/attendance/current", { signal });
}

// Request either the server-selected current month or a specific calendar month.
export function getMonthlyAttendanceCalendar(
  year?: number,
  month?: number,
  signal?: AbortSignal,
): Promise<AttendanceCalendar> {
  // Only send a date query when both values are present; the API rejects partial year/month pairs.
  const query = year === undefined || month === undefined
    ? ""
    : `?year=${year}&month=${month}`;
  return apiRequest<AttendanceCalendar>(`/api/attendance/calendar${query}`, { signal });
}

// Ask the server to create Clock In using its synchronized external clock, not a browser timestamp.
export function clockInNow(): Promise<AttendancePunchResult> {
  return apiRequest<AttendancePunchResult>("/api/attendance/clock-in", {
    method: "POST",
    antiforgery: true,
  });
}

// Ask the server to close the current open shift using the same synchronized time source.
export function clockOutNow(): Promise<AttendancePunchResult> {
  return apiRequest<AttendancePunchResult>("/api/attendance/clock-out", {
    method: "POST",
    antiforgery: true,
  });
}
