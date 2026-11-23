import { apiRequest } from "../../lib/apiClient";

export type AttendanceCorrectionRequest = {
  workDate: string;
  clockInLocalTime?: string;
  clockOutLocalTime?: string;
  clockOutNextDay: boolean;
};

export type PendingAttendanceCorrection = {
  requestId: string;
  employeeName: string;
  employeeNumber: string | null;
  workDate: string;
  timeZoneId: string;
  requestedClockInAtLocal: string | null;
  requestedClockOutAtLocal: string | null;
  submittedAtLocal: string;
  status: string;
  rejectionReason: string | null;
  isAttentionRequired: boolean;
};

export function submitAttendanceCorrection(
  request: AttendanceCorrectionRequest,
): Promise<{ requestId: string; status: "PendingApproval" }> {
  return apiRequest("/api/attendance/corrections", {
    method: "POST",
    body: request,
    antiforgery: true,
  });
}

export function getPendingAttendanceCorrections(
  signal?: AbortSignal,
): Promise<PendingAttendanceCorrection[]> {
  return apiRequest<PendingAttendanceCorrection[]>("/api/attendance/corrections/pending", { signal });
}

export function approveAttendanceCorrection(requestId: string): Promise<void> {
  return apiRequest<void>(`/api/attendance/corrections/${requestId}/approve`, {
    method: "POST",
    antiforgery: true,
  });
}

export function rejectAttendanceCorrection(requestId: string, reason: string): Promise<void> {
  return apiRequest<void>(`/api/attendance/corrections/${requestId}/reject`, {
    method: "POST",
    body: { reason },
    antiforgery: true,
  });
}
