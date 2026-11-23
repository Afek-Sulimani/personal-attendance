import { apiRequest } from "../../lib/apiClient";

export type InvitationManager = {
  membershipId: string;
  displayName: string;
  employeeNumber: string | null;
};

export type CreateEmployeeInvitationInput = {
  email: string;
  displayName: string;
  managerMembershipId: string;
  roles: string[];
};

export type CreatedEmployeeInvitation = {
  invitationId: string;
  activationUrl: string;
  expiresAtUtc: string;
};

export function getInvitationManagers(signal?: AbortSignal): Promise<InvitationManager[]> {
  return apiRequest<InvitationManager[]>("/api/employee-invitations/managers", { signal });
}

export function createEmployeeInvitation(
  invitation: CreateEmployeeInvitationInput,
): Promise<CreatedEmployeeInvitation> {
  return apiRequest<CreatedEmployeeInvitation>("/api/employee-invitations", {
    method: "POST",
    body: invitation,
    antiforgery: true,
  });
}
