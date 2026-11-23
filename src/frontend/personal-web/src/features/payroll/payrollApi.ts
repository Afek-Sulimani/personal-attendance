import { apiRequest } from "../../lib/apiClient";

export type PayrollEmployee = {
  membershipId: string;
  displayName: string;
  employeeNumber: string | null;
};

export type MonthlyWorkSchedule = {
  employeeMembershipId: string;
  year: number;
  month: number;
  isConfigured: boolean;
  workingDates: string[];
};

export type CompanyPayrollSettings = {
  isConfigured: boolean;
  payrollPeriodStartDay: number | null;
  timeZoneId: string;
  defaultWorkStartTime: string | null;
  defaultWorkEndTime: string | null;
  defaultWorkDays: string[];
  updatedAtUtc: string | null;
};

export type CompanyPayrollSettingsInput = {
  payrollPeriodStartDay: number;
  timeZoneId: string;
  defaultWorkStartTime: string;
  defaultWorkEndTime: string;
  defaultWorkDays: string[];
};

export function getCompanyPayrollSettings(signal?: AbortSignal): Promise<CompanyPayrollSettings> {
  return apiRequest<CompanyPayrollSettings>("/api/payroll/company-settings", { signal });
}

export function saveCompanyPayrollSettings(
  settings: CompanyPayrollSettingsInput,
): Promise<CompanyPayrollSettings> {
  return apiRequest<CompanyPayrollSettings>("/api/payroll/company-settings", {
    method: "PUT",
    body: settings,
    antiforgery: true,
  });
}

export function getPayrollEmployees(signal?: AbortSignal): Promise<PayrollEmployee[]> {
  return apiRequest<PayrollEmployee[]>("/api/payroll/employees", { signal });
}

export function getMonthlyWorkSchedule(
  employeeMembershipId: string,
  year: number,
  month: number,
  signal?: AbortSignal,
): Promise<MonthlyWorkSchedule> {
  const query = new URLSearchParams({ year: String(year), month: String(month) });
  return apiRequest<MonthlyWorkSchedule>(
    `/api/payroll/employees/${employeeMembershipId}/work-days?${query.toString()}`,
    { signal },
  );
}

export function saveMonthlyWorkSchedule(
  employeeMembershipId: string,
  year: number,
  month: number,
  workingDates: string[],
): Promise<MonthlyWorkSchedule> {
  const query = new URLSearchParams({ year: String(year), month: String(month) });
  return apiRequest<MonthlyWorkSchedule>(
    `/api/payroll/employees/${employeeMembershipId}/work-days?${query.toString()}`,
    {
      method: "PUT",
      body: { workingDates },
      antiforgery: true,
    },
  );
}
