import { apiRequest } from "../../lib/apiClient";

export type InvitationDetails = {
  email: string;
  displayName: string;
  companyCode: string;
  expiresAtUtc: string;
};

export type InvitationAcceptance = {
  companyCode: string;
};

export function validateInvitation(
  token: string,
  signal?: AbortSignal,
): Promise<InvitationDetails> {
  return apiRequest<InvitationDetails>("/api/employee-invitations/validate", {
    method: "POST",
    body: { token },
    signal,
  });
}

export function acceptInvitation(
  token: string,
  password?: string,
): Promise<InvitationAcceptance> {
  return apiRequest<InvitationAcceptance>("/api/employee-invitations/accept", {
    method: "POST",
    body: password === undefined ? { token } : { token, password },
    antiforgery: true,
  });
}
