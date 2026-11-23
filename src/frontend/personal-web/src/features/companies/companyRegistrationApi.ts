import { apiRequest } from "../../lib/apiClient";

export type CompanyRegistrationRequest = {
  companyName: string;
  companyRepresentativeName: string;
  companyRepresentativeEmail: string;
};

export type CompanyRegistrationResult = {
  companyName: string;
  companyCode: string;
  companyRepresentativeName: string;
  companyRepresentativeEmail: string;
  activationUrl: string;
};

export function registerCompany(
  request: CompanyRegistrationRequest,
): Promise<CompanyRegistrationResult> {
  return apiRequest<CompanyRegistrationResult>("/api/companies/registration", {
    method: "POST",
    body: request,
    antiforgery: true,
  });
}
