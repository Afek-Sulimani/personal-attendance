const companyZone = "Asia/Jerusalem";

export function latestPastWorkday(lookbackWorkdays = 1): { date: string; label: string } {
  const now = new Date();
  const localDate = new Intl.DateTimeFormat("en-CA", {
    year: "numeric", month: "2-digit", day: "2-digit", timeZone: companyZone,
  }).format(now);
  const day = new Date(`${localDate}T12:00:00Z`);
  for (let remaining = lookbackWorkdays; remaining > 0; remaining--) {
    do {
      day.setUTCDate(day.getUTCDate() - 1);
    } while (day.getUTCDay() === 5);
  }
  return {
    date: day.toISOString().slice(0, 10),
    label: new Intl.DateTimeFormat("en-US", {
      weekday: "short", month: "short", day: "numeric", year: "numeric", timeZone: "UTC",
    }).format(day),
  };
}
