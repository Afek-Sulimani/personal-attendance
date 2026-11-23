import { expect, test } from "@playwright/test";
import { AttendancePage } from "../pages/AttendancePage";
import { useActor } from "../support/sharedWorkspace";

test("EMP-12: rejected correction permits today's live Clock In", async ({ page }, testInfo) => {
  await useActor(page, "Employee");
  // Exercise the UI contract independently of other scenarios' persisted punches.
  await page.route("**/api/attendance/current", async route => {
    const response = await route.fetch();
    await route.fulfill({ response, json: { ...await response.json(), hasOpenShift: false, openShift: null } });
  });
  let today = "";
  await page.route("**/api/attendance/calendar*", async route => {
    const response = await route.fetch();
    const calendar = await response.json();
    today = calendar.todayLocalDate;
    calendar.scheduleConfigured = true;
    calendar.days = calendar.days.map((day: { date: string }) => day.date === today
      ? { ...day, status: "CorrectionRejected", correctionStatus: "Rejected", isWorkDay: true } : day);
    await route.fulfill({ response, json: calendar });
  });
  const attendance = new AttendancePage(page);
  await attendance.open();
  await expect(page.getByText("A correction request was rejected. You can submit a new request.")).toBeVisible();
  await expect(page.getByRole("button", { name: "Clock In now" })).toBeEnabled();
  await attendance.capture(testInfo, "rejected-correction-live-clock-enabled");
});
