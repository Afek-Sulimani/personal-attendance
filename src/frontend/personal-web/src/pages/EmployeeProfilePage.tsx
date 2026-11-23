import LoadingIndicator from "../components/LoadingIndicator";
import { useEffect, useState } from "react";
import type { FormEvent } from "react";
import { useNavigate } from "react-router-dom";
import ApplicationLayout from "../components/ApplicationLayout";
import { getCurrentUser, signOut } from "../features/auth/authApi";
import type { AuthenticatedUser } from "../features/auth/authApi";
import { changeMyPassword, getMyProfile } from "../features/profile/profileApi";
import type { EmployeeProfile } from "../features/profile/profileApi";
import { ApiRequestError } from "../lib/apiClient";

function formatRole(role: string): string {
  switch (role) {
    case "PayrollRepresentative": return "Payroll";
    case "TeamLead": return "Team lead";
    default: return role;
  }
}

export default function EmployeeProfilePage() {
  const navigate = useNavigate();
  const [user, setUser] = useState<AuthenticatedUser | null>(null);
  const [profile, setProfile] = useState<EmployeeProfile | null>(null);
  const [isLoading, setIsLoading] = useState(true);
  const [isSavingPassword, setIsSavingPassword] = useState(false);
  const [isSigningOut, setIsSigningOut] = useState(false);
  const [currentPassword, setCurrentPassword] = useState("");
  const [newPassword, setNewPassword] = useState("");
  const [confirmPassword, setConfirmPassword] = useState("");
  const [errorMessage, setErrorMessage] = useState("");

  useEffect(() => {
    const controller = new AbortController();
    let isCurrent = true;

    Promise.all([getCurrentUser(controller.signal), getMyProfile(controller.signal)])
      .then(([currentUser, employeeProfile]) => {
        if (!isCurrent) return;
        setUser(currentUser);
        setProfile(employeeProfile);
      })
      .catch(error => {
        if (!isCurrent) return;
        if (error instanceof ApiRequestError && error.status === 401) {
          navigate("/login", { replace: true });
          return;
        }
        setErrorMessage(error instanceof Error ? error.message : "Profile information could not be loaded.");
      })
      .finally(() => {
        if (isCurrent) setIsLoading(false);
      });

    return () => {
      isCurrent = false;
      controller.abort();
    };
  }, [navigate]);

  async function handlePasswordChange(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setErrorMessage("");

    if (newPassword !== confirmPassword) {
      setErrorMessage("The new passwords do not match.");
      return;
    }

    setIsSavingPassword(true);
    try {
      await changeMyPassword(currentPassword, newPassword);
      setCurrentPassword("");
      setNewPassword("");
      setConfirmPassword("");
      navigate("/login", {
        replace: true,
        state: {
          passwordChanged: true,
          email: profile?.email ?? user?.email,
          companyCode: user?.companyCode,
        },
      });
    } catch (error) {
      setErrorMessage(error instanceof Error ? error.message : "The password could not be changed.");
    } finally {
      setIsSavingPassword(false);
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
    return <main className="attendance-loading-page"><p role="status">{errorMessage || <LoadingIndicator label="Loading your profile…" />}</p></main>;
  }

  return (
    <ApplicationLayout
      user={user}
      isSigningOut={isSigningOut}
      onSignOut={() => void handleSignOut()}
    >
      <div className="attendance-page-content employee-profile-page">
        <header className="attendance-page-heading">
          <div>
            <p className="attendance-eyebrow">ACCOUNT</p>
            <h1>My profile</h1>
            <p className="attendance-page-description">View your account and company employment details.</p>
          </div>
        </header>

        {errorMessage && <div className="status-banner error" role="alert">{errorMessage}</div>}
        {profile && (
          <>
            <section className="employee-profile-card" aria-labelledby="account-details-title">
              <div className="employee-profile-section-heading">
                <div>
                  <p className="attendance-eyebrow">ACCOUNT DETAILS</p>
                  <h2 id="account-details-title">Sign-in information</h2>
                </div>
              </div>
              <dl className="employee-profile-details">
                <div><dt>Email</dt><dd>{profile.email}</dd></div>
                <div><dt>Username</dt><dd>{profile.userName}</dd></div>
              </dl>
            </section>

            <section className="employee-profile-card" aria-labelledby="employment-title">
              <div className="employee-profile-section-heading">
                <div>
                  <p className="attendance-eyebrow">COMPANY MEMBERSHIPS</p>
                  <h2 id="employment-title">Employment details</h2>
                </div>
              </div>
              {profile.companies.length === 0 ? (
                <p className="employee-profile-empty">No active company memberships were found.</p>
              ) : (
                <div className="employee-company-list">
                  {profile.companies.map(company => (
                    <article className="employee-company-card" key={company.membershipId}>
                      <div className="employee-company-heading">
                        <div>
                          <h3>{company.companyName}</h3>
                          <p>{company.displayName}</p>
                        </div>
                        <span className="employee-company-code">Company {company.companyCode}</span>
                      </div>
                      <dl className="employee-profile-details">
                        <div><dt>Employee number</dt><dd>{company.employeeNumber ?? "Not assigned"}</dd></div>
                        <div><dt>Company ID</dt><dd>{company.companyCode}</dd></div>
                        <div><dt>Roles</dt><dd>{company.roles.map(formatRole).join(", ") || "None"}</dd></div>
                        <div>
                          <dt>Pay type</dt>
                          <dd>{company.compensationType ?? "Not configured"}</dd>
                        </div>
                        {company.compensationType === "Hourly" && (
                          <div><dt>Hourly rate</dt><dd>{company.hourlyRate?.toFixed(2) ?? "Not configured"}</dd></div>
                        )}
                      </dl>
                    </article>
                  ))}
                </div>
              )}
            </section>

            <section className="employee-profile-card" aria-labelledby="change-password-title">
              <div className="employee-profile-section-heading">
                <div>
                  <p className="attendance-eyebrow">SECURITY</p>
                  <h2 id="change-password-title">Change password</h2>
                </div>
              </div>
              <form className="employee-password-form" onSubmit={event => void handlePasswordChange(event)}>
                <div className="form-field">
                  <label htmlFor="current-password">Current password</label>
                  <input id="current-password" name="currentPassword" type="password" autoComplete="current-password" value={currentPassword} onChange={event => setCurrentPassword(event.target.value)} required maxLength={256} />
                </div>
                <div className="form-field">
                  <label htmlFor="profile-new-password">New password</label>
                  <input id="profile-new-password" name="newPassword" type="password" autoComplete="new-password" value={newPassword} onChange={event => setNewPassword(event.target.value)} required minLength={6} maxLength={256} />
                </div>
                <div className="form-field">
                  <label htmlFor="confirm-new-password">Confirm new password</label>
                  <input id="confirm-new-password" name="confirmNewPassword" type="password" autoComplete="new-password" value={confirmPassword} onChange={event => setConfirmPassword(event.target.value)} required minLength={6} maxLength={256} />
                </div>
                <p className="field-hint">Use at least 6 characters with an uppercase letter, a lowercase letter, a number, and a symbol. After a successful change, you will need to sign in again on all devices.</p>
                <button aria-busy={isSavingPassword} className="primary-button" type="submit" disabled={isSavingPassword}>
                  {isSavingPassword ? <LoadingIndicator label="Changing password…" /> : "Change password"}
                </button>
              </form>
            </section>
          </>
        )}
      </div>
    </ApplicationLayout>
  );
}
