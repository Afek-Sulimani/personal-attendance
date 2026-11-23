import { apiRequest } from "../lib/apiClient";
import LoadingIndicator from "../components/LoadingIndicator";
import { useEffect, useState } from "react";
import type { FormEvent } from "react";
import { Link, useLocation, useNavigate } from "react-router-dom";
import AuthLayout from "../components/AuthLayout";
import { useActivationFlow } from "../features/invitations/ActivationFlowContext";
import { acceptInvitation } from "../features/invitations/invitationApi";
import { getCurrentUser, signIn, signOut } from "../features/auth/authApi";
import type { AuthenticatedUser } from "../features/auth/authApi";

type LoginRouteState = {
  companyCode?: string;
  invitedCompanyCode?: string;
  email?: string;
  invitationAccepted?: boolean;
  passwordChanged?: boolean;
  continueInvitationAfterLogin?: boolean;
};

export default function LoginPage() {
  const location = useLocation();
  const navigate = useNavigate();
  const routeState = location.state as LoginRouteState | null;
  const { token: pendingActivationToken, setToken: setPendingActivationToken } = useActivationFlow();

  const [companyIdentifier, setCompanyIdentifier] = useState(
    routeState?.continueInvitationAfterLogin ? "" : routeState?.companyCode ?? "",
  );
  const [googleEnabled, setGoogleEnabled] = useState(false);
  useEffect(() => { apiRequest<{enabled:boolean}>("/api/auth/google/status").then(result => setGoogleEnabled(result.enabled)).catch(() => undefined); }, []);
  const [email, setEmail] = useState(routeState?.email ?? "");
  const [password, setPassword] = useState("");
  const [currentUser, setCurrentUser] = useState<AuthenticatedUser | null>(null);
  const [isCheckingSession, setIsCheckingSession] = useState(true);
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [errorMessage, setErrorMessage] = useState("");
  const [statusMessage, setStatusMessage] = useState(
    routeState?.invitationAccepted
      ? `Invitation accepted for company ${routeState.companyCode ?? ""}. Sign in to continue.`
      : routeState?.passwordChanged
        ? "Your password has been changed. Sign in with your new password."
        : "",
  );

  useEffect(() => {
    const controller = new AbortController();
    getCurrentUser(controller.signal)
      .then(user => {
        setCurrentUser(user);
        if (!routeState?.continueInvitationAfterLogin) {
          navigate("/attendance", { replace: true });
        }
      })
      .catch(() => undefined)
      .finally(() => {
        if (!controller.signal.aborted) setIsCheckingSession(false);
      });

    return () => controller.abort();
  }, [navigate, routeState?.continueInvitationAfterLogin]);

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setErrorMessage("");
    setStatusMessage("");
    setIsSubmitting(true);

    try {
      const user = await signIn({ companyIdentifier, email, password });
      setCurrentUser(user);

      if (routeState?.continueInvitationAfterLogin) {
        if (!pendingActivationToken) {
          throw new Error("Reopen the invitation link to finish accepting this invitation.");
        }

        const acceptedInvitation = await acceptInvitation(pendingActivationToken);
        setPendingActivationToken(null);
        setPassword("");

        try {
          await signOut();
          setCurrentUser(null);
          setCompanyIdentifier(acceptedInvitation.companyCode);
          setEmail(routeState.email ?? user.email);
          setStatusMessage(
            `Invitation accepted for company ${acceptedInvitation.companyCode}. Sign in with its name or ID.`,
          );
          navigate("/login", {
            replace: true,
            state: {
              invitationAccepted: true,
              companyCode: acceptedInvitation.companyCode,
              email: routeState.email ?? user.email,
            } satisfies LoginRouteState,
          });
        } catch {
          setStatusMessage(
            `Invitation accepted for company ${acceptedInvitation.companyCode}. Sign out, then sign in with its name or ID.`,
          );
          setCompanyIdentifier(acceptedInvitation.companyCode);
          setEmail(routeState.email ?? user.email);
          navigate("/login", {
            replace: true,
            state: {
              invitationAccepted: true,
              companyCode: acceptedInvitation.companyCode,
              email: routeState.email ?? user.email,
            } satisfies LoginRouteState,
          });
        }

        return;
      }

      setPassword("");
      navigate("/attendance", { replace: true });
    } catch (error) {
      setErrorMessage(error instanceof Error ? error.message : "Sign in could not be completed.");
    } finally {
      setIsSubmitting(false);
    }
  }

  async function handleSignOut() {
    setErrorMessage("");
    setStatusMessage("");
    setIsSubmitting(true);

    try {
      await signOut();
      setCurrentUser(null);
      setStatusMessage("You have signed out.");
    } catch (error) {
      setErrorMessage(error instanceof Error ? error.message : "Sign out could not be completed.");
    } finally {
      setIsSubmitting(false);
    }
  }

  return (
    <AuthLayout>
      <div className="auth-heading">
        <p className="auth-eyebrow">EMPLOYEE WORKSPACE</p>
        <h1>Welcome back</h1>
        <p className="auth-description">Sign in to access your Personal Attendance account.</p>
      </div>

      {routeState?.continueInvitationAfterLogin && (
        <div className="status-banner information" role="note">
          Sign in with the name or ID of a company where you already have an account. After accepting, you can sign in to company {routeState.invitedCompanyCode ?? "from the invitation"}.
        </div>
      )}

      {isCheckingSession && (
        <p className="session-check" role="status"><LoadingIndicator label="Checking your existing session…" /></p>
      )}

      {currentUser && (
        <section className="session-panel" aria-label="Current signed-in account">
          <div>
            <p className="session-label">CURRENT SESSION</p>
            <strong>{currentUser.displayName}</strong>
            <span>{currentUser.email} · {currentUser.companyCode}</span>
          </div>
          <button
            className="text-button"
            type="button"
            onClick={() => void handleSignOut()}
            disabled={isSubmitting}
          >
            Sign out
          </button>
        </section>
      )}

      {statusMessage && <div className="status-banner success" role="status">{statusMessage}</div>}
      {errorMessage && <div className="status-banner error" role="alert">{errorMessage}</div>}

      {currentUser && statusMessage.startsWith("You are signed in") ? (
        <div className="next-step-panel">
          <strong>Your session is active.</strong>
          <p>The attendance workspace will be available here in a later step.</p>
        </div>
      ) : (
        <form className="auth-form" onSubmit={event => void handleSubmit(event)}>
          <div className="form-field">
            <label htmlFor="company-identifier">Company name or ID</label>
            <input
              id="company-identifier"
              name="companyIdentifier"
              type="text"
              autoComplete="organization"
              value={companyIdentifier}
              onChange={event => setCompanyIdentifier(event.target.value)}
              required
              maxLength={200}
              placeholder={routeState?.continueInvitationAfterLogin
                ? "Existing account company name or ID"
                : "Enter your company name or 5-digit ID"}
            />
            <small>Enter the exact company name, or use its ID if the name is shared by multiple companies.</small>
          </div>

          <div className="form-field">
            <label htmlFor="login-email">Email</label>
            <input
              id="login-email"
              name="email"
              type="email"
              autoComplete="username"
              value={email}
              onChange={event => setEmail(event.target.value)}
              readOnly={routeState?.continueInvitationAfterLogin === true}
              required
              maxLength={256}
              placeholder="name@company.com"
            />
          </div>

          <div className="form-field">
            <label htmlFor="login-password">Password</label>
            <input
              id="login-password"
              name="password"
              type="password"
              autoComplete="current-password"
              value={password}
              onChange={event => setPassword(event.target.value)}
              required
              maxLength={256}
              placeholder="Enter your password"
            />
          </div>

          <button aria-busy={isSubmitting} className="primary-button" type="submit" disabled={isSubmitting}>
            {isSubmitting ? <LoadingIndicator label="Signing in…" /> : "Sign in"}
          </button>
        </form>
      )}

      {googleEnabled && <button type="button" className="secondary-attendance-button" disabled={!companyIdentifier.trim() || isSubmitting} onClick={() => { window.location.assign(`/api/auth/google/start?companyIdentifier=${encodeURIComponent(companyIdentifier.trim())}`); }}>Continue with Google</button>}
      {new URLSearchParams(location.search).has("googleError") && <p role="alert">Google sign-in failed. Check your active account and company, then try again.</p>}

      <p className="auth-note">
        New employee? Use the activation link provided by your HR or payroll contact.
      </p>
      <p className="auth-note auth-register-note">
        Company contact? <Link to="/register-company">Register a company</Link>
      </p>
    </AuthLayout>
  );
}
