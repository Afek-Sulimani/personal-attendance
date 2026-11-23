import LoadingIndicator from "../components/LoadingIndicator";
import { useCallback, useEffect, useMemo, useRef, useState } from "react";
import type { FormEvent } from "react";
import { useNavigate } from "react-router-dom";
import ApplicationLayout from "../components/ApplicationLayout";
import CreatedInvitationPanel from "../features/invitations/CreatedInvitationPanel";
import type { CreatedInvitationDetails } from "../features/invitations/CreatedInvitationPanel";
import ManagerCombobox from "../features/invitations/ManagerCombobox";
import { getCurrentUser, signOut } from "../features/auth/authApi";
import type { AuthenticatedUser } from "../features/auth/authApi";
import {
  createEmployeeInvitation,
  getInvitationManagers,
} from "../features/invitations/employeeInvitationsAdminApi";
import type { InvitationManager } from "../features/invitations/employeeInvitationsAdminApi";
import { ApiRequestError } from "../lib/apiClient";

const roleOptions = [
  ["Employee", "Employee"],
  ["TeamLead", "Team lead"],
  ["HR", "HR"],
  ["PayrollRepresentative", "Payroll"],
  ["Admin", "Admin"],
] as const;

function getAssignableRoles(roles: string[]): typeof roleOptions[number][0][] {
  if (roles.includes("Admin")) {
    return roleOptions.map(([role]) => role);
  }
  if (roles.includes("HR") || roles.includes("PayrollRepresentative")) {
    return ["Employee", "TeamLead", "HR", "PayrollRepresentative"];
  }
  return ["Employee"];
}

export default function EmployeeInvitationsPage() {
  const navigate = useNavigate();
  const [user, setUser] = useState<AuthenticatedUser | null>(null);
  const [managers, setManagers] = useState<InvitationManager[]>([]);
  const [email, setEmail] = useState("");
  const [displayName, setDisplayName] = useState("");
  const [managerMembershipId, setManagerMembershipId] = useState("");
  const [selectedRoles, setSelectedRoles] = useState<Set<string>>(new Set(["Employee"]));
  const [createdInvitation, setCreatedInvitation] = useState<CreatedInvitationDetails | null>(null);
  const submissionInProgress = useRef(false);
  const [isLoading, setIsLoading] = useState(true);
  const [isCreating, setIsCreating] = useState(false);
  const [isSigningOut, setIsSigningOut] = useState(false);
  const [errorMessage, setErrorMessage] = useState("");

  const assignableRoles = useMemo(() => getAssignableRoles(user?.roles ?? []), [user?.roles]);

  const handleError = useCallback((error: unknown) => {
    if (error instanceof ApiRequestError && error.status === 401) {
      navigate("/login", { replace: true });
      return;
    }
    setErrorMessage(error instanceof Error ? error.message : "The employee invitation could not be created.");
  }, [navigate]);

  useEffect(() => {
    const controller = new AbortController();
    let isCurrent = true;

    async function loadPage() {
      try {
        const [currentUser, availableManagers] = await Promise.all([
          getCurrentUser(controller.signal),
          getInvitationManagers(controller.signal),
        ]);
        if (!isCurrent) return;
        setUser(currentUser);
        setManagers(availableManagers);
        setManagerMembershipId(
          availableManagers.some(manager => manager.membershipId === currentUser.membershipId)
            ? currentUser.membershipId
            : availableManagers[0]?.membershipId ?? "",
        );
      } catch (error) {
        if (isCurrent) handleError(error);
      } finally {
        if (isCurrent) setIsLoading(false);
      }
    }

    void loadPage();
    return () => {
      isCurrent = false;
      controller.abort();
    };
  }, [handleError]);

  useEffect(() => {
    setSelectedRoles(current => {
      const allowed = [...current].filter(role => assignableRoles.includes(role as typeof assignableRoles[number]));
      return new Set(allowed.length > 0 ? allowed : ["Employee"]);
    });
  }, [assignableRoles]);

  function toggleRole(role: string) {
    setSelectedRoles(current => {
      const updated = new Set(current);
      if (updated.has(role)) updated.delete(role);
      else if (role === "Employee") return new Set([role]);
      else {
        updated.delete("Employee");
        updated.add(role);
      }
      return updated;
    });
  }

  async function handleCreate(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (submissionInProgress.current || createdInvitation) return;
    if (!managerMembershipId || selectedRoles.size === 0) {
      setErrorMessage("Choose a direct manager and at least one role.");
      return;
    }

    submissionInProgress.current = true;
    setIsCreating(true);
    setErrorMessage("");
    const submittedName = displayName.trim();
    const submittedEmail = email.trim();
    const submittedManagerId = managerMembershipId;
    const submittedRoles = [...selectedRoles];
    try {
      const invitation = await createEmployeeInvitation({
        email: submittedEmail,
        displayName: submittedName,
        managerMembershipId: submittedManagerId,
        roles: submittedRoles,
      });
      setCreatedInvitation({
        activationUrl: invitation.activationUrl,
        displayName: submittedName,
        email: submittedEmail,
        managerName: managers.find(manager => manager.membershipId === submittedManagerId)?.displayName ?? "Selected manager",
        roleNames: roleOptions.filter(([role]) => submittedRoles.includes(role)).map(([, label]) => label),
      });
    } catch (error) {
      handleError(error);
    } finally {
      submissionInProgress.current = false;
      setIsCreating(false);
    }
  }

  function handleAddAnother() {
    setCreatedInvitation(null);
    setDisplayName("");
    setEmail("");
    setManagerMembershipId(
      managers.some(manager => manager.membershipId === user?.membershipId)
        ? user!.membershipId
        : managers[0]?.membershipId ?? "",
    );
    setSelectedRoles(new Set(["Employee"]));
    setErrorMessage("");
  }

  async function handleSignOut() {
    setIsSigningOut(true);
    try {
      await signOut();
      navigate("/login", { replace: true });
    } catch (error) {
      handleError(error);
    } finally {
      setIsSigningOut(false);
    }
  }

  if (isLoading || !user) {
    return <main className="attendance-loading-page"><p role="status">{errorMessage || <LoadingIndicator label="Loading people management…" />}</p></main>;
  }

  return (
    <ApplicationLayout user={user} isSigningOut={isSigningOut} onSignOut={() => void handleSignOut()}>
      <div className="attendance-page-content">
        <header className="attendance-page-heading">
          <div>
            <p className="attendance-eyebrow">COMPANY PEOPLE</p>
            <h1>{createdInvitation ? "Invitation created" : "Add a company member"}</h1>
            <p className="attendance-page-description">{createdInvitation
              ? "Review the invitation and share its activation link with the employee."
              : "Create an employee profile, assign roles, and share the activation link yourself."}</p>
          </div>
        </header>

        {errorMessage && <div className="status-banner error" role="alert">{errorMessage}</div>}
        {createdInvitation ? (
          <CreatedInvitationPanel invitation={createdInvitation} onAddAnother={handleAddAnother} />
        ) : (
          <form className="payroll-workday-card people-invitation-form" onSubmit={event => void handleCreate(event)}>
          <div className="settings-form-grid">
            <label className="form-field">
              <span>Employee name</span>
              <input required maxLength={200} value={displayName} onChange={event => setDisplayName(event.target.value)} autoComplete="name" />
            </label>
            <label className="form-field">
              <span>Employee email</span>
              <input required type="email" maxLength={256} value={email} onChange={event => setEmail(event.target.value)} autoComplete="email" />
            </label>
            <ManagerCombobox
              managers={managers}
              selectedId={managerMembershipId}
              onChange={setManagerMembershipId}
              disabled={isCreating || managers.length === 0}
            />
          </div>

          <fieldset className="company-weekdays-fieldset">
            <legend>Company roles</legend>
            <p>Roles can be combined for a single employee.</p>
            <div className="company-weekdays-grid role-options-grid">
              {roleOptions.filter(([role]) => assignableRoles.includes(role)).map(([role, label]) => (
                <label className="company-weekday-option" key={role}>
                  <input type="checkbox" checked={selectedRoles.has(role)} onChange={() => toggleRole(role)} />
                  <span>{label}</span>
                </label>
              ))}
            </div>
          </fieldset>

          <button aria-busy={isCreating} className="primary-attendance-button" type="submit" disabled={isCreating || managers.length === 0}>
            {isCreating ? <LoadingIndicator label="Creating invitation…" /> : "Create activation link"}
          </button>
          {managers.length === 0 && (
            <p className="field-hint" role="status">An active Admin, Team Lead, or HR member must be available as the direct manager.</p>
          )}
          </form>
        )}
      </div>
    </ApplicationLayout>
  );
}
