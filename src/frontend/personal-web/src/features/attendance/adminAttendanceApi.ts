import { apiRequest } from "../../lib/apiClient";

export type AdminAttendanceShift = {
  workShiftId: string;
  workDate: string;
  employeeName: string;
  employeeNumber: string | null;
  clockInAtLocal: string;
  clockInTimeZoneId: string;
  clockInSource: string;
  clockOutAtLocal: string | null;
  clockOutTimeZoneId: string | null;
  clockOutSource: string | null;
};

export type AdminAttendanceChange = {
  auditId: string;
  changedByMembershipId: string;
  changedByName: string;
  previousWorkDate: string;
  workDate: string;
  employeeName: string;
  employeeNumber: string | null;
  previousClockInAtLocal: string;
  previousClockInTimeZoneId: string;
  previousClockInSource: string;
  previousClockInProvider: string | null;
  previousClockInProviderSampleUtc: string | null;
  clockInAtLocal: string;
  clockInTimeZoneId: string;
  clockInSource: string;
  clockInProvider: string | null;
  clockInProviderSampleUtc: string | null;
  previousClockOutAtLocal: string | null;
  previousClockOutTimeZoneId: string | null;
  previousClockOutSource: string | null;
  previousClockOutProvider: string | null;
  previousClockOutProviderSampleUtc: string | null;
  clockOutAtLocal: string | null;
  clockOutTimeZoneId: string | null;
  clockOutSource: string | null;
  clockOutProvider: string | null;
  clockOutProviderSampleUtc: string | null;
  reason: string;
  changedAtLocal: string;
  changeTimeZoneId: string;
  changeTimeProvider: string;
  changeProviderSampleUtc: string;
};

export type AdminAttendanceMonth = {
  employeeMembershipId: string;
  year: number;
  month: number;
  shifts: AdminAttendanceShift[];
  changes: AdminAttendanceChange[];
};

export type UpdateAdminAttendanceInput = {
  clockInLocalDateTime: string;
  clockOutLocalDateTime?: string | null;
  reason: string;
};

export function getAdminAttendanceMonth(
  employeeMembershipId: string,
  year: number,
  month: number,
  signal?: AbortSignal,
): Promise<AdminAttendanceMonth> {
  const query = new URLSearchParams({ employeeMembershipId, year: String(year), month: String(month) });
  return apiRequest<AdminAttendanceMonth>(`/api/admin/attendance?${query.toString()}`, { signal });
}

export function updateAdminAttendanceShift(
  workShiftId: string,
  update: UpdateAdminAttendanceInput,
): Promise<AdminAttendanceChange> {
  return apiRequest<AdminAttendanceChange>(`/api/admin/attendance/${workShiftId}`, {
    method: "PUT",
    body: update,
    antiforgery: true,
  });
}
