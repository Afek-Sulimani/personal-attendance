import { apiRequest } from "../../lib/apiClient";

export type CompensationType = "Global" | "Hourly";

export type CompanyEmployment = {
  membershipId: string;
  companyName: string;
  companyCode: string;
  displayName: string;
  employeeNumber: string | null;
  roles: string[];
  compensationType: CompensationType | null;
  hourlyRate: number | null;
};

export type EmployeeProfile = {
  email: string;
  userName: string;
  companies: CompanyEmployment[];
};

export type ManagedEmployeeProfile = {
  membershipId: string;
  displayName: string;
  email: string;
  employeeNumber: string | null;
  compensationType: CompensationType | null;
  hourlyRate: number | null;
};

export function getMyProfile(signal?: AbortSignal): Promise<EmployeeProfile> {
  return apiRequest("/api/profile", { signal });
}

export function changeMyPassword(currentPassword: string, newPassword: string): Promise<void> {
  return apiRequest("/api/profile/password", {
    method: "POST",
    body: { currentPassword, newPassword },
    antiforgery: true,
  });
}

export function getCompanyEmployeeProfiles(signal?: AbortSignal): Promise<ManagedEmployeeProfile[]> {
  return apiRequest("/api/profile/company-employees", { signal });
}

export function updateEmployeeCompensation(
  membershipId: string,
  compensationType: CompensationType,
  hourlyRate: number | null,
): Promise<ManagedEmployeeProfile> {
  return apiRequest(`/api/profile/company-employees/${membershipId}/compensation`, {
    method: "PUT",
    body: { compensationType, hourlyRate },
    antiforgery: true,
  });
}
