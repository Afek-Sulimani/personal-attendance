import LoadingIndicator from "../../components/LoadingIndicator";
import type { AttendanceCalendar as AttendanceCalendarData, AttendanceCalendarDay } from "./attendanceApi";

type AttendanceCalendarProps = {
  calendar: AttendanceCalendarData;
  selectedDate: string;
  onSelectDate: (date: string) => void;
  onChangeMonth: (direction: -1 | 1) => void;
  isLoading: boolean;
};

const weekDays = ["Sun", "Mon", "Tue", "Wed", "Thu", "Fri", "Sat"];

function formatMonth(year: number, month: number): string {
  return new Intl.DateTimeFormat("en-US", {
    month: "long",
    year: "numeric",
    timeZone: "UTC",
  }).format(new Date(Date.UTC(year, month - 1, 15)));
}

function getCalendarCells(calendar: AttendanceCalendarData): Array<AttendanceCalendarDay | null> {
  const firstWeekday = new Date(Date.UTC(calendar.year, calendar.month - 1, 1)).getUTCDay();
  const cells: Array<AttendanceCalendarDay | null> = [
    ...Array.from({ length: firstWeekday }, () => null),
    ...calendar.days,
  ];
  const trailingCells = (7 - (cells.length % 7)) % 7;
  cells.push(...Array.from({ length: trailingCells }, () => null));
  return cells;
}

function statusLabel(status: AttendanceCalendarDay["status"]): string {
  switch (status) {
    case "Complete": return "Clock in and clock out completed";
    case "Open": return "Shift is open";
    case "Missing": return "Past workday with no attendance recorded";
    case "Future": return "Future date";
    case "NonWorking": return "Not a scheduled workday";
    case "NoPunchToday": return "No punch recorded today";
    case "CorrectionPending": return "Correction is awaiting manager approval";
    case "CorrectionRejected": return "Correction was rejected";
    case "ScheduleNotConfigured": return "Work schedule is not configured";
  }
}

function dotClass(status: AttendanceCalendarDay["status"]): string | null {
  switch (status) {
    case "Complete": return "attendance-dot is-complete";
    case "Open": return "attendance-dot is-open";
    case "Missing":
    case "CorrectionPending":
    case "CorrectionRejected": return "attendance-dot needs-attention";
    case "Future":
    case "NonWorking": return "attendance-dot is-neutral";
    case "NoPunchToday":
    case "ScheduleNotConfigured": return null;
  }
}

export default function AttendanceCalendar({
  calendar,
  selectedDate,
  onSelectDate,
  onChangeMonth,
  isLoading,
}: AttendanceCalendarProps) {
  const cells = getCalendarCells(calendar);
  const monthLabel = formatMonth(calendar.year, calendar.month);

  return (
    <section className="attendance-calendar-card" aria-label="Monthly attendance calendar">
      <div className="attendance-calendar-header">
        <div>
          <p className="attendance-eyebrow">MONTHLY VIEW</p>
          <h2>{monthLabel}</h2>
          {isLoading && <span className="calendar-refresh-status" role="status"><LoadingIndicator label="Refreshing…" /></span>}
        </div>
        <div className="attendance-month-controls">
          <button
            className="calendar-nav-button"
            type="button"
            aria-label="Previous month"
            onClick={() => onChangeMonth(-1)}
            disabled={isLoading}
          >
            ‹
          </button>
          <button
            className="calendar-nav-button"
            type="button"
            aria-label="Next month"
            onClick={() => onChangeMonth(1)}
            disabled={isLoading}
          >
            ›
          </button>
        </div>
      </div>

      <div className="attendance-weekdays" aria-hidden="true">
        {weekDays.map(day => <span key={day}>{day}</span>)}
      </div>

      <div className={`attendance-calendar-grid${isLoading ? " is-loading" : ""}`}>
        {cells.map((day, index) => {
          if (!day) {
            return <span className="attendance-calendar-empty" key={`empty-${index}`} aria-hidden="true" />;
          }

          const date = day.date;
          const isToday = date === calendar.todayLocalDate;
          const isSelected = date === selectedDate;
          const isOutsideCurrentPayrollPeriod = Boolean(
            calendar.currentPayrollPeriodStartDate &&
            calendar.currentPayrollPeriodEndDate &&
            (date < calendar.currentPayrollPeriodStartDate || date > calendar.currentPayrollPeriodEndDate),
          );
          const dayNumber = Number(date.slice(-2));
          const circleClass = [
            "attendance-date-button",
            isToday ? "is-today" : "",
            isSelected ? "is-selected" : "",
            isOutsideCurrentPayrollPeriod ? "is-outside-payroll-period" : "",
          ].filter(Boolean).join(" ");

          return (
            <button
              className={circleClass}
              type="button"
              key={date}
              aria-label={`${date}: ${statusLabel(day.status)}${isOutsideCurrentPayrollPeriod ? ", outside the current payroll period" : ""}${isToday ? `, today in ${calendar.timeZoneId}` : ""}`}
              aria-pressed={isSelected}
              title={isOutsideCurrentPayrollPeriod ? "Outside the current payroll period" : undefined}
              onClick={() => onSelectDate(date)}
            >
              <span className="attendance-date-number">{dayNumber}</span>
              {dotClass(day.status) && <span className={dotClass(day.status)!} aria-hidden="true" />}
            </button>
          );
        })}
      </div>

      <div className="attendance-calendar-legend" aria-label="Calendar status legend">
        <span><i className="attendance-dot is-complete" aria-hidden="true" /> Complete</span>
        <span><i className="attendance-dot is-open" aria-hidden="true" /> Open shift</span>
        <span><i className="attendance-dot needs-attention" aria-hidden="true" /> Attention</span>
        <span><i className="attendance-dot is-neutral" aria-hidden="true" /> Future / non-workday</span>
        <span><i className="attendance-period-muted-key" aria-hidden="true" /> Outside payroll period</span>
      </div>
    </section>
  );
}
