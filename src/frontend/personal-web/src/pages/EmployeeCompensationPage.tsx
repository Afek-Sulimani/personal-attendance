import LoadingIndicator from "../components/LoadingIndicator";
import { useEffect, useMemo, useState } from "react";
import { useNavigate } from "react-router-dom";
import ApplicationLayout from "../components/ApplicationLayout";
import { getCurrentUser, signOut } from "../features/auth/authApi";
import type { AuthenticatedUser } from "../features/auth/authApi";
import {
  getCompanyEmployeeProfiles,
  updateEmployeeCompensation,
} from "../features/profile/profileApi";
import type {
  CompensationType,
  ManagedEmployeeProfile,
} from "../features/profile/profileApi";
import { ApiRequestError } from "../lib/apiClient";
import { matchesEmployeeSearch } from "../lib/matchesEmployeeSearch";

type CompensationDraft = {
  compensationType: CompensationType | "";
  hourlyRate: string;
};

export default function EmployeeCompensationPage() {
  const navigate = useNavigate();
  const [user, setUser] = useState<AuthenticatedUser | null>(null);
  const [employees, setEmployees] = useState<ManagedEmployeeProfile[]>([]);
  const [drafts, setDrafts] = useState<Record<string, CompensationDraft>>({});
  const [search, setSearch] = useState("");
  const [isLoading, setIsLoading] = useState(true);
  const [savingMembershipId, setSavingMembershipId] = useState("");
  const [errorMessage, setErrorMessage] = useState("");
  const [statusMessage, setStatusMessage] = useState("");
  const [isSigningOut, setIsSigningOut] = useState(false);

  useEffect(() => {
    const controller = new AbortController();
    let isCurrent = true;

    Promise.all([getCurrentUser(controller.signal), getCompanyEmployeeProfiles(controller.signal)])
      .then(([currentUser, companyEmployees]) => {
        if (!isCurrent) return;
        setUser(currentUser);
        setEmployees(companyEmployees);
        setDrafts(Object.fromEntries(companyEmployees.map(employee => [employee.membershipId, {
          compensationType: employee.compensationType ?? "",
          hourlyRate: employee.hourlyRate?.toFixed(2) ?? "",
        }])));
      })
      .catch(error => {
        if (!isCurrent) return;
        if (error instanceof ApiRequestError && error.status === 401) {
          navigate("/login", { replace: true });
          return;
        }
        if (error instanceof ApiRequestError && error.status === 403) {
          setErrorMessage("You do not have access to employee pay details.");
          return;
        }
        setErrorMessage(error instanceof Error ? error.message : "Employee profiles could not be loaded.");
      })
      .finally(() => {
        if (isCurrent) setIsLoading(false);
      });

    return () => {
      isCurrent = false;
      controller.abort();
    };
  }, [navigate]);

  const filteredEmployees = useMemo(() => {
    const query = search.trim().toLocaleLowerCase();
    if (!query) return employees;
    return employees.filter(employee =>
      matchesEmployeeSearch(employee.displayName, employee.employeeNumber, search) ||
      employee.email.toLocaleLowerCase().includes(query),
    );
  }, [employees, search]);

  function updateDraft(membershipId: string, patch: Partial<CompensationDraft>) {
    setDrafts(current => ({
      ...current,
      [membershipId]: { ...current[membershipId], ...patch },
    }));
    setStatusMessage("");
  }

  async function saveCompensation(employee: ManagedEmployeeProfile) {
    const draft = drafts[employee.membershipId];
    if (!draft?.compensationType) {
      setErrorMessage("Choose a pay type before saving.");
      return;
    }

    const hourlyRate = draft.compensationType === "Hourly" ? Number(draft.hourlyRate) : null;
    if (draft.compensationType === "Hourly" && (!Number.isFinite(hourlyRate) || hourlyRate === null || hourlyRate <= 0)) {
      setErrorMessage("Enter a positive hourly rate.");
      return;
    }

    setErrorMessage("");
    setStatusMessage("");
    setSavingMembershipId(employee.membershipId);
    try {
      const updatedEmployee = await updateEmployeeCompensation(
        employee.membershipId,
        draft.compensationType,
        hourlyRate,
      );
      setEmployees(current => current.map(item => item.membershipId === updatedEmployee.membershipId ? updatedEmployee : item));
      setDrafts(current => ({
        ...current,
        [updatedEmployee.membershipId]: {
          compensationType: updatedEmployee.compensationType ?? "",
          hourlyRate: updatedEmployee.hourlyRate?.toFixed(2) ?? "",
        },
      }));
      setStatusMessage(`Pay details saved for ${updatedEmployee.displayName}.`);
    } catch (error) {
      setErrorMessage(error instanceof Error ? error.message : "Pay details could not be saved.");
    } finally {
      setSavingMembershipId("");
    }
  }

  async function handleSignOut() {
    setIsSigningOut(true);
    try {
      await signOut();
      navigate("/login", { replace: true });
    } catch (error) {
      setErrorMessage(error instanceof Error ? error.message : "Sign out could not be completed.");
    } finally {
      setIsSigningOut(false);
    }
  }

  if (isLoading || !user) {
    return <main className="attendance-loading-page"><p role="status">{errorMessage || <LoadingIndicator label="Loading employee profiles…" />}</p></main>;
  }

  return (
    <ApplicationLayout
      user={user}
      isSigningOut={isSigningOut}
      onSignOut={() => void handleSignOut()}
    >
      <div className="attendance-page-content employee-compensation-page">
        <header className="attendance-page-heading">
          <div>
            <p className="attendance-eyebrow">COMPANY PEOPLE</p>
            <h1>Employee pay details</h1>
            <p className="attendance-page-description">Set whether each employee is paid a global salary or an hourly rate.</p>
          </div>
        </header>

        {errorMessage && <div className="status-banner error" role="alert">{errorMessage}</div>}
        {statusMessage && <div className="status-banner success" role="status">{statusMessage}</div>}
        {
          <section className="employee-compensation-card" aria-label="Company employee pay settings">
            <div className="employee-compensation-toolbar">
              <p>{employees.length} employee{employees.length === 1 ? "" : "s"}</p>
              <label className="employee-profile-search">
                <span className="visually-hidden">Search employees</span>
                <input value={search} onChange={event => setSearch(event.target.value)} placeholder="Search by name, email, or employee number" />
              </label>
            </div>

            {filteredEmployees.length === 0 ? (
              <p className="employee-profile-empty">No employees match this search.</p>
            ) : (
              <div className="employee-compensation-list">
                {filteredEmployees.map(employee => {
                  const draft = drafts[employee.membershipId] ?? { compensationType: "", hourlyRate: "" };
                  const isSaving = savingMembershipId === employee.membershipId;
                  return (
                    <article className="employee-compensation-row" key={employee.membershipId}>
                      <div className="employee-compensation-identity">
                        <h2>{employee.displayName}</h2>
                        <p>{employee.email}{employee.employeeNumber ? ` · ${employee.employeeNumber}` : ""}</p>
                      </div>
                      <div className="employee-compensation-controls">
                        <label className="form-field">
                          <span>Pay type</span>
                          <select value={draft.compensationType} onChange={event => updateDraft(employee.membershipId, { compensationType: event.target.value as CompensationType | "" })}>
                            <option value="">Select type</option>
                            <option value="Global">Global</option>
                            <option value="Hourly">Hourly</option>
                          </select>
                        </label>
                        {draft.compensationType === "Hourly" && (
                          <label className="form-field">
                            <span>Hourly rate (number only)</span>
                            <input type="number" min="0.01" max="9999999999999999.99" step="0.01" inputMode="decimal" value={draft.hourlyRate} onChange={event => updateDraft(employee.membershipId, { hourlyRate: event.target.value })} required />
                          </label>
                        )}
                        <button aria-busy={isSaving} className="secondary-attendance-button" type="button" disabled={isSaving || !draft.compensationType} onClick={() => void saveCompensation(employee)}>
                          {isSaving ? <LoadingIndicator label="Saving…" /> : "Save pay details"}
                        </button>
                      </div>
                    </article>
                  );
                })}
              </div>
            )}
          </section>
        }
      </div>
    </ApplicationLayout>
  );
}
