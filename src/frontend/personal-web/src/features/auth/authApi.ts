import { apiRequest } from "../../lib/apiClient";

export type LoginCredentials = {
  companyIdentifier: string;
  email: string;
  password: string;
};

export type AuthenticatedUser = {
  userId: string;
  email: string;
  displayName: string;
  companyId: string;
  companyCode: string;
  membershipId: string;
  employeeProfileId: string;
  employeeNumber: string | null;
  roles: string[];
};

export function signIn(credentials: LoginCredentials): Promise<AuthenticatedUser> {
  return apiRequest<AuthenticatedUser>("/api/auth/login", {
    method: "POST",
    body: credentials,
    antiforgery: true,
  });
}

export function getCurrentUser(signal?: AbortSignal): Promise<AuthenticatedUser> {
  return apiRequest<AuthenticatedUser>("/api/auth/me", { signal });
}

export function signOut(): Promise<void> {
  return apiRequest<void>("/api/auth/logout", {
    method: "POST",
    antiforgery: true,
  });
}
