import LoadingIndicator from "../../components/LoadingIndicator";
import { useEffect, useState } from "react";
import type { FormEvent } from "react";
import type { AdminAttendanceShift } from "./adminAttendanceApi";

function toDateTimeInput(value: string | null): string {
  return value ? value.slice(0, 16) : "";
}

export default function AdminAttendanceShiftEditor({
  shift,
  isSaving,
  onSave,
}: {
  shift: AdminAttendanceShift;
  isSaving: boolean;
  onSave: (input: { clockInLocalDateTime: string; clockOutLocalDateTime?: string; reason: string }) => void;
}) {
  const [clockIn, setClockIn] = useState(toDateTimeInput(shift.clockInAtLocal));
  const [clockOut, setClockOut] = useState(toDateTimeInput(shift.clockOutAtLocal));
  const [reason, setReason] = useState("");

  useEffect(() => {
    setClockIn(toDateTimeInput(shift.clockInAtLocal));
    setClockOut(toDateTimeInput(shift.clockOutAtLocal));
    setReason("");
  }, [shift.clockInAtLocal, shift.clockOutAtLocal]);

  function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    onSave({
      clockInLocalDateTime: `${clockIn}:00`,
      clockOutLocalDateTime: shift.clockOutAtLocal === null ? undefined : `${clockOut}:00`,
      reason: reason.trim(),
    });
  }

  return (
    <form className="admin-attendance-shift-card" onSubmit={handleSubmit}>
      <div className="admin-attendance-shift-heading">
        <div>
          <strong>{shift.workDate}</strong>
          <span>{shift.employeeName}{shift.employeeNumber ? ` · ${shift.employeeNumber}` : ""}</span>
        </div>
        <span className={`attendance-shift-state${shift.clockOutAtLocal ? " is-complete" : " is-open"}`}>
          {shift.clockOutAtLocal ? "Complete" : "Open shift"}
        </span>
      </div>

      <div className="admin-attendance-time-grid">
        <label className="form-field">
          <span>Clock In · {shift.clockInTimeZoneId}</span>
          <input
            type="datetime-local"
            step="60"
            required
            value={clockIn}
            onChange={event => setClockIn(event.target.value)}
          />
        </label>
        {shift.clockOutAtLocal !== null && (
          <label className="form-field">
            <span>Clock Out · {shift.clockOutTimeZoneId}</span>
            <input
              type="datetime-local"
              step="60"
              required
              value={clockOut}
              onChange={event => setClockOut(event.target.value)}
            />
          </label>
        )}
      </div>

      <label className="form-field">
        <span>Reason for Admin change</span>
        <textarea required minLength={1} maxLength={1000} value={reason} onChange={event => setReason(event.target.value)} />
      </label>

      <div className="admin-attendance-shift-footer">
        <span>Current source: {shift.clockInSource}{shift.clockOutSource ? ` / ${shift.clockOutSource}` : ""}</span>
        <button aria-busy={isSaving} className="secondary-attendance-button" type="submit" disabled={isSaving || !reason.trim()}>
          {isSaving ? <LoadingIndicator label="Saving audited change…" /> : "Save with audit reason"}
        </button>
      </div>
    </form>
  );
}
