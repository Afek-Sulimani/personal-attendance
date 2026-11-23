import LoadingIndicator from "../components/LoadingIndicator";
import { useCallback, useEffect, useState } from "react";
import { useNavigate } from "react-router-dom";
import ApplicationLayout from "../components/ApplicationLayout";
import {
  approveAttendanceCorrection,
  getPendingAttendanceCorrections,
  rejectAttendanceCorrection,
} from "../features/attendance/correctionApi";
import type { PendingAttendanceCorrection } from "../features/attendance/correctionApi";
import CorrectionReviewTable from "../features/attendance/CorrectionReviewTable";
import { getCurrentUser, signOut } from "../features/auth/authApi";
import type { AuthenticatedUser } from "../features/auth/authApi";
import { ApiRequestError } from "../lib/apiClient";

export default function AttendanceCorrectionReviewPage() {
  const navigate = useNavigate();
  const [user, setUser] = useState<AuthenticatedUser | null>(null);
  const [requests, setRequests] = useState<PendingAttendanceCorrection[]>([]);
  const [activeRequestId, setActiveRequestId] = useState<string | null>(null);
  const [isLoading, setIsLoading] = useState(true);
  const [isSigningOut, setIsSigningOut] = useState(false);
  const [errorMessage, setErrorMessage] = useState("");
  const [statusMessage, setStatusMessage] = useState("");

  const loadRequests = useCallback(async (signal?: AbortSignal) => {
    const pending = await getPendingAttendanceCorrections(signal);
    setRequests(pending);
    window.dispatchEvent(new Event("attendance-corrections-updated"));
  }, []);

  const handleError = useCallback((error: unknown) => {
    if (error instanceof ApiRequestError && error.status === 401) {
      navigate("/login", { replace: true });
      return;
    }
    setErrorMessage(error instanceof Error ? error.message : "Correction requests could not be loaded.");
  }, [navigate]);

  useEffect(() => {
    const controller = new AbortController();
    let isCurrent = true;

    async function loadPage() {
      try {
        const [currentUser, pending] = await Promise.all([
          getCurrentUser(controller.signal),
          getPendingAttendanceCorrections(controller.signal),
        ]);
        if (!isCurrent) return;
        setUser(currentUser);
        setRequests(pending);
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

  async function handleApprove(requestId: string) {
    setActiveRequestId(requestId);
    setErrorMessage("");
    setStatusMessage("");
    try {
      await approveAttendanceCorrection(requestId);
      setStatusMessage("Correction approved and attendance updated.");
      await loadRequests();
    } catch (error) {
      handleError(error);
    } finally {
      setActiveRequestId(null);
    }
  }

  async function handleReject(requestId: string, reason: string) {
    setActiveRequestId(requestId);
    setErrorMessage("");
    setStatusMessage("");
    try {
      await rejectAttendanceCorrection(requestId, reason);
      setStatusMessage("Correction rejected. The reason was recorded.");
      await loadRequests();
    } catch (error) {
      handleError(error);
    } finally {
      setActiveRequestId(null);
    }
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
    return <main className="attendance-loading-page"><p role="status">{errorMessage || <LoadingIndicator label="Loading correction requests…" />}</p></main>;
  }

  return (
    <ApplicationLayout user={user} isSigningOut={isSigningOut} onSignOut={() => void handleSignOut()}>
      <div className="attendance-page-content">
        <header className="attendance-page-heading">
          <div>
            <p className="attendance-eyebrow">MANAGER REVIEW</p>
            <h1>Attendance corrections</h1>
            <p className="attendance-page-description">Review requests from your direct reports and eligible company representatives.</p>
          </div>
          <span className="attendance-timezone-badge">Request time zones are shown per request</span>
        </header>

        {errorMessage && <div className="status-banner error" role="alert">{errorMessage}</div>}
        {statusMessage && <div className="status-banner success" role="status">{statusMessage}</div>}

        {requests.length === 0 ? (
          <section className="correction-empty-state" role="status">
            <h2>No pending correction requests</h2>
            <p>New requests from employees who report to you will appear here.</p>
          </section>
        ) : (
          <CorrectionReviewTable requests={requests} activeRequestId={activeRequestId}
            onApprove={requestId => void handleApprove(requestId)}
            onReject={(requestId, reason) => void handleReject(requestId, reason)} />
        )}
      </div>
    </ApplicationLayout>
  );
}
