export function formatReportDate(date: string, options: Intl.DateTimeFormatOptions = { dateStyle: "medium" }): string {
  return new Intl.DateTimeFormat("en-US", { ...options, timeZone: "UTC" })
    .format(new Date(`${date}T12:00:00.000Z`));
}

export function formatReportTime(value: string | null, timeZoneId: string): string {
  if (!value) return "—";
  return new Intl.DateTimeFormat("en-GB", {
    hour: "2-digit",
    minute: "2-digit",
    timeZone: timeZoneId,
  }).format(new Date(value));
}

export function formatReportDateTime(value: string, timeZoneId: string): string {
  return new Intl.DateTimeFormat("en-US", {
    dateStyle: "medium",
    timeStyle: "short",
    timeZone: timeZoneId,
  }).format(new Date(value));
}

export function formatWorkedTime(minutes: number | null | undefined): string {
  if (minutes === null || minutes === undefined || !Number.isFinite(minutes) || minutes < 0) return "—";
  const hours = Math.floor(minutes / 60);
  const remainingMinutes = minutes % 60;
  return `${hours}h ${String(remainingMinutes).padStart(2, "0")}m`;
}

export function formatReportPeriod(start: string, end: string): string {
  return `${formatReportDate(start, { day: "numeric", month: "short", year: "numeric" })} – ${formatReportDate(end, { day: "numeric", month: "short", year: "numeric" })}`;
}

export function payrollMonthKey(date: string): number {
  const [year, month] = date.split("-").map(Number);
  return year * 12 + month - 1;
}

export function monthKeyToPeriod(key: number): { year: number; month: number } {
  const year = Math.floor(key / 12);
  return { year, month: key - year * 12 + 1 };
}
