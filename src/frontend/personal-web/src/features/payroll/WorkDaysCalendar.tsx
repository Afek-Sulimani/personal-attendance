type WorkDaysCalendarProps = {
  year: number;
  month: number;
  todayLocalDate: string;
  workingDates: ReadonlySet<string>;
};

const weekDays = ["Sun", "Mon", "Tue", "Wed", "Thu", "Fri", "Sat"];

export default function WorkDaysCalendar({
  year,
  month,
  todayLocalDate,
  workingDates,
}: WorkDaysCalendarProps) {
  const firstWeekday = new Date(Date.UTC(year, month - 1, 1)).getUTCDay();
  const daysInMonth = new Date(Date.UTC(year, month, 0)).getUTCDate();
  const cells: Array<number | null> = [
    ...Array.from({ length: firstWeekday }, () => null),
    ...Array.from({ length: daysInMonth }, (_, index) => index + 1),
  ];
  cells.push(...Array.from({ length: (7 - (cells.length % 7)) % 7 }, () => null));

  return (
    <div className="workdays-calendar" aria-label="Preview of this employee's working days">
      <div className="attendance-weekdays" aria-hidden="true">
        {weekDays.map(day => <span key={day}>{day}</span>)}
      </div>
      <div className="workdays-calendar-grid">
        {cells.map((day, index) => {
          if (day === null) {
            return <span className="attendance-calendar-empty" key={`empty-${index}`} aria-hidden="true" />;
          }

          const date = `${year}-${String(month).padStart(2, "0")}-${String(day).padStart(2, "0")}`;
          const isSelectedWorkDay = workingDates.has(date);
          const isToday = date === todayLocalDate;
          const className = [
            "workday-date-button",
            isSelectedWorkDay ? "is-workday" : "",
            isToday ? "is-today" : "",
          ].filter(Boolean).join(" ");

          return (
            <span
              className={className}
              key={date}
              aria-label={`${date}${isToday ? ", today" : ""}${isSelectedWorkDay ? ", scheduled workday" : ", not scheduled"}`}
            >
              {day}
            </span>
          );
        })}
      </div>
    </div>
  );
}
