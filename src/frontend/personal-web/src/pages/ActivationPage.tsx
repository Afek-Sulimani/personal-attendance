import LoadingIndicator from "../components/LoadingIndicator";
import { useEffect, useRef, useState } from "react";
import type { FormEvent } from "react";
import { Link, useNavigate } from "react-router-dom";
import AuthLayout from "../components/AuthLayout";
import { useActivationFlow } from "../features/invitations/ActivationFlowContext";
import {
  acceptInvitation,
  validateInvitation,
} from "../features/invitations/invitationApi";
import type { InvitationDetails } from "../features/invitations/invitationApi";
import { getCurrentUser, signOut } from "../features/auth/authApi";
import type { AuthenticatedUser } from "../features/auth/authApi";
import { ApiRequestError } from "../lib/apiClient";

type LoginRouteState = {
  companyCode: string;
  invitedCompanyCode?: string;
  email: string;
  continueInvitationAfterLogin?: boolean;
  invitationAccepted?: boolean;
};

export default function ActivationPage() {
  const navigate = useNavigate();
  const { token, setToken } = useActivationFlow();
  const fragmentHandled = useRef(false);
  const [invitation, setInvitation] = useState<InvitationDetails | null>(null);
  const [signedInUser, setSignedInUser] = useState<AuthenticatedUser | null>(null);
  const [isValidating, setIsValidating] = useState(true);
  const [isCheckingSession, setIsCheckingSession] = useState(false);
  const [isAccepting, setIsAccepting] = useState(false);
  const [isSwitchingAccount, setIsSwitchingAccount] = useState(false);
  const [errorMessage, setErrorMessage] = useState("");
  const [password, setPassword] = useState("");
  const [passwordConfirmation, setPasswordConfirmation] = useState("");

  useEffect(() => {
    if (fragmentHandled.current) return;
    fragmentHandled.current = true;

    const tokenFromLink = new URLSearchParams(window.location.hash.slice(1)).get("token");
    if (tokenFromLink) {
      setToken(tokenFromLink);
      window.history.replaceState(
        window.history.state,
        "",
        `${window.location.pathname}${window.location.search}`,
      );
    } else if (!token) {
      setErrorMessage("No invitation token was found. Open the full activation link you received.");
      setIsValidating(false);
    }
  }, [setToken, token]);

  useEffect(() => {
    if (!token) return;
    const invitationToken = token;

    const controller = new AbortController();
    let isCurrent = true;
    setIsValidating(true);
    setErrorMessage("");

    async function loadInvitation() {
      try {
        const details = await validateInvitation(invitationToken, controller.signal);
        if (!isCurrent) return;
        setInvitation(details);
        setIsCheckingSession(true);

        try {
          const user = await getCurrentUser(controller.signal);
          if (isCurrent) setSignedInUser(user);
        } catch (error) {
          if (!(error instanceof ApiRequestError && error.status === 401) && isCurrent) {
            setErrorMessage("The current session could not be checked. You can still activate the invitation.");
          }
        } finally {
          if (isCurrent) setIsCheckingSession(false);
        }
      } catch (error) {
        if (isCurrent) {
          setInvitation(null);
          setErrorMessage(error instanceof Error ? error.message : "The invitation could not be checked.");
        }
      } finally {
        if (isCurrent) setIsValidating(false);
      }
    }

    void loadInvitation();
    return () => {
      isCurrent = false;
      controller.abort();
    };
  }, [token]);

  function navigateToSignIn(continueWithInvitation: boolean) {
    if (!invitation) return;
    navigate("/login", {
      state: {
        companyCode: continueWithInvitation ? "" : invitation.companyCode,
        invitedCompanyCode: invitation.companyCode,
        email: invitation.email,
        continueInvitationAfterLogin: continueWithInvitation,
      } satisfies LoginRouteState,
    });
  }

  function finishAcceptance(companyCode: string) {
    const email = invitation?.email ?? "";
    setToken(null);
    navigate("/login", {
      replace: true,
      state: {
        companyCode,
        email,
        invitationAccepted: true,
      } satisfies LoginRouteState,
    });
  }

  async function handleCreateAccount(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setErrorMessage("");

    if (password !== passwordConfirmation) {
      setErrorMessage("The passwords do not match.");
      return;
    }
    if (!token) {
      setErrorMessage("This invitation link is no longer available. Open the full link again.");
      return;
    }

    setIsAccepting(true);
    try {
      const result = await acceptInvitation(token, password);
      setPassword("");
      setPasswordConfirmation("");
      await finishAcceptance(result.companyCode);
    } catch (error) {
      if (error instanceof ApiRequestError && error.status === 409 &&
          error.message.toLowerCase().includes("sign in")) {
        setErrorMessage("An account already exists for this email. Sign in to that account to accept the invitation.");
        return;
      }
      setErrorMessage(error instanceof Error ? error.message : "The account could not be activated.");
    } finally {
      setIsAccepting(false);
    }
  }

  async function acceptAsSignedInUser() {
    if (!token || !invitation) return;
    setErrorMessage("");
    setIsAccepting(true);

    try {
      const result = await acceptInvitation(token);
      setToken(null);
      try {
        await signOut();
      } catch {
        setErrorMessage(
          `The invitation was accepted for ${result.companyCode}, but sign out did not complete. Sign out manually before switching companies.`,
        );
        navigate("/login", {
          replace: true,
          state: {
            companyCode: result.companyCode,
            email: invitation.email,
            invitationAccepted: true,
          } satisfies LoginRouteState,
        });
        setIsAccepting(false);
        return;
      }
      await finishAcceptance(result.companyCode);
    } catch (error) {
      setErrorMessage(error instanceof Error ? error.message : "The invitation could not be accepted.");
    } finally {
      setIsAccepting(false);
    }
  }

  async function switchToInvitedAccount() {
    if (!invitation) return;
    setErrorMessage("");
    setIsSwitchingAccount(true);

    try {
      if (signedInUser) await signOut();
      setSignedInUser(null);
      navigateToSignIn(true);
    } catch (error) {
      setErrorMessage(error instanceof Error ? error.message : "The current account could not be signed out.");
    } finally {
      setIsSwitchingAccount(false);
    }
  }

  const currentEmailMatchesInvitation = signedInUser?.email.toLocaleLowerCase() ===
    invitation?.email.toLocaleLowerCase();

  return (
    <AuthLayout>
      <div className="auth-heading">
        <p className="auth-eyebrow">EMPLOYEE ONBOARDING</p>
        <h1>Activate account</h1>
        <p className="auth-description">Review your invitation and finish setting up your account.</p>
      </div>

      {isValidating && (
        <p className="session-check" role="status"><LoadingIndicator label="Checking your invitation…" /></p>
      )}

      {errorMessage && <div className="status-banner error" role="alert">{errorMessage}</div>}

      {invitation && !isValidating && (
        <>
          <section className="invitation-summary" aria-label="Invitation details">
            <p className="invitation-summary-label">INVITATION FOR</p>
            <strong>{invitation.displayName}</strong>
            <span>{invitation.email}</span>
            <span className="invitation-company">Company ID: {invitation.companyCode}</span>
          </section>

          {isCheckingSession && (
            <p className="session-check" role="status"><LoadingIndicator label="Checking your current account…" /></p>
          )}

          {signedInUser && currentEmailMatchesInvitation && (
            <div className="activation-choice">
              <p>You are signed in as <strong>{signedInUser.email}</strong>, the account invited here.</p>
              <button aria-busy={isAccepting}
                className="primary-button"
                type="button"
                onClick={() => void acceptAsSignedInUser()}
                disabled={isAccepting}
              >
                {isAccepting ? <LoadingIndicator label="Accepting invitation…" /> : "Accept invitation"}
              </button>
            </div>
          )}

          {signedInUser && !currentEmailMatchesInvitation && (
            <div className="activation-choice">
              <p>
                You are signed in as <strong>{signedInUser.email}</strong>. This invitation is for a different email.
              </p>
              <button aria-busy={isSwitchingAccount}
                className="primary-button"
                type="button"
                onClick={() => void switchToInvitedAccount()}
                disabled={isSwitchingAccount}
              >
                {isSwitchingAccount ? <LoadingIndicator label="Switching account…" /> : "Sign in with the invited account"}
              </button>
            </div>
          )}

          {!signedInUser && (
            <form className="auth-form" onSubmit={event => void handleCreateAccount(event)}>
              <div className="form-field">
                <label htmlFor="new-password">Choose a password</label>
                <input
                  id="new-password"
                  name="newPassword"
                  type="password"
                  autoComplete="new-password"
                  value={password}
                  onChange={event => setPassword(event.target.value)}
                  required
                  minLength={6}
                  maxLength={256}
                  placeholder="Create your password"
                />
              </div>

              <div className="form-field">
                <label htmlFor="confirm-password">Confirm password</label>
                <input
                  id="confirm-password"
                  name="confirmPassword"
                  type="password"
                  autoComplete="new-password"
                  value={passwordConfirmation}
                  onChange={event => setPasswordConfirmation(event.target.value)}
                  required
                  minLength={6}
                  maxLength={256}
                  placeholder="Enter the same password again"
                />
              </div>

              <p className="field-hint">Use at least 6 characters with an uppercase letter, a lowercase letter, a number, and a symbol. The server validates these requirements.</p>
              <button aria-busy={isAccepting} className="primary-button" type="submit" disabled={isAccepting || isCheckingSession}>
                {isAccepting ? <LoadingIndicator label="Activating account…" /> : "Activate account"}
              </button>
              <button
                className="text-button activation-signin"
                type="button"
                onClick={() => navigateToSignIn(true)}
              >
                Already have an account? Sign in to accept this invitation.
              </button>
            </form>
          )}
        </>
      )}

      {!isValidating && !invitation && !errorMessage && (
        <div className="status-banner error" role="alert">
          This invitation link is missing, expired, or has already been used.
        </div>
      )}

      <p className="auth-note">
        <Link to="/login">Return to sign in</Link>
      </p>
    </AuthLayout>
  );
}
