import LoadingIndicator from "../../components/LoadingIndicator";
import { useState } from "react";
import type { FormEvent } from "react";
import type { PendingAttendanceCorrection } from "./correctionApi";

type Props = {
  requests: PendingAttendanceCorrection[];
  activeRequestId: string | null;
  onApprove: (requestId: string) => void;
  onReject: (requestId: string, reason: string) => void;
};

function formatDate(date: string): string {
  return new Intl.DateTimeFormat("en-US", { dateStyle: "medium", timeZone: "UTC" })
    .format(new Date(`${date}T12:00:00.000Z`));
}

function formatTime(value: string | null, timeZoneId: string): string {
  if (!value) return "—";
  return new Intl.DateTimeFormat("en-GB", {
    day: "numeric", month: "short", hour: "2-digit", minute: "2-digit", timeZone: timeZoneId,
  }).format(new Date(value));
}

export default function CorrectionReviewTable({ requests, activeRequestId, onApprove, onReject }: Props) {
  const [rejectingRequestId, setRejectingRequestId] = useState<string | null>(null);
  const [reason, setReason] = useState("");
  const groups = new Map<string, PendingAttendanceCorrection[]>();
  for (const request of requests) {
    const key = request.employeeNumber ?? request.employeeName;
    const group = groups.get(key) ?? [];
    group.push(request);
    groups.set(key, group);
  }

  function submitRejection(event: FormEvent<HTMLFormElement>, requestId: string) {
    event.preventDefault();
    if (!reason.trim()) return;
    onReject(requestId, reason.trim());
    setRejectingRequestId(null);
    setReason("");
  }

  return (
    <div className="correction-review-groups">
      {[...groups.entries()].map(([key, group]) => (
        <section className="correction-review-group" key={key} aria-label={`Pending corrections for ${group[0].employeeName}`}>
          <div className="correction-review-group-heading">
            <div>
              <h2>{group[0].employeeName}</h2>
              {group[0].employeeNumber && <span>Employee {group[0].employeeNumber}</span>}
              <span className="correction-review-pending-label">
                {group.some(request => request.isAttentionRequired) && <i aria-hidden="true" />}
                {group.length} awaiting approval
              </span>
            </div>
          </div>
          <div className="correction-review-table-wrap" role="region" aria-label={`Requests for ${group[0].employeeName}`} tabIndex={0}>
            <table className="correction-review-table">
              <thead><tr><th scope="col">Date</th><th scope="col">Clock In</th><th scope="col">Clock Out</th><th scope="col">Received</th><th scope="col">Action</th></tr></thead>
              <tbody>{group.map(request => (
                <tr key={request.requestId} className="correction-review-row">
                  <td>{formatDate(request.workDate)}</td>
                  <td>{formatTime(request.requestedClockInAtLocal, request.timeZoneId)}</td>
                  <td>{formatTime(request.requestedClockOutAtLocal, request.timeZoneId)}</td>
                  <td>{formatTime(request.submittedAtLocal, request.timeZoneId)}<small>{request.timeZoneId}</small></td>
                  <td>
                    <div className="correction-review-row-actions">
                      <button aria-busy={activeRequestId === request.requestId} type="button" className="correction-approve-button" onClick={() => onApprove(request.requestId)} disabled={activeRequestId !== null}>
                        {activeRequestId === request.requestId ? <LoadingIndicator label="Saving…" /> : "Approve"}
                      </button>
                      {rejectingRequestId !== request.requestId && (
                        <button type="button" className="correction-reject-button" onClick={() => { setRejectingRequestId(request.requestId); setReason(""); }} disabled={activeRequestId !== null}>Reject</button>
                      )}
                    </div>
                    {rejectingRequestId === request.requestId && (
                      <form className="correction-rejection-form" onSubmit={event => submitRejection(event, request.requestId)}>
                        <label htmlFor={`reason-${request.requestId}`}>Reason for rejection</label>
                        <input id={`reason-${request.requestId}`} value={reason} onChange={event => setReason(event.target.value)} maxLength={1000} required autoFocus />
                        <div className="correction-review-row-actions">
                          <button aria-busy={activeRequestId === request.requestId} type="submit" className="correction-reject-button" disabled={!reason.trim() || activeRequestId !== null}>{activeRequestId === request.requestId ? <LoadingIndicator label="Saving…" /> : "Reject with reason"}</button>
                          <button type="button" onClick={() => setRejectingRequestId(null)}>Cancel</button>
                        </div>
                      </form>
                    )}
                  </td>
                </tr>
              ))}</tbody>
            </table>
          </div>
        </section>
      ))}
    </div>
  );
}
