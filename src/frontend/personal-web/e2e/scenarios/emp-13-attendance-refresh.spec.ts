import { expect, test } from "@playwright/test";
import { AttendancePage } from "../pages/AttendancePage";
import { useActor } from "../support/sharedWorkspace";

test("EMP-13: refreshes on focus and every minute using server responses", async ({ page }, testInfo) => {
  await useActor(page, "Employee");
  await page.clock.install();
  let requests = 0;
  await page.route("**/api/attendance/calendar*", async route => {
    const response = await route.fetch();
    const calendar = await response.json();
    requests++;
    // Simulate another tab changing the selected day's state on the server.
    calendar.days = calendar.days.map((day: { date: string }) => day.date === calendar.todayLocalDate
      ? { ...day, status: requests > 1 ? "CorrectionRejected" : "NoPunchToday", isWorkDay: true } : day);
    await route.fulfill({ response, json: calendar });
  });
  const attendance = new AttendancePage(page);
  await attendance.open();
  await expect.poll(() => requests).toBeGreaterThan(0);
  const initialRequests = requests;
  await page.evaluate(() => window.dispatchEvent(new Event("focus")));
  await expect.poll(() => requests).toBeGreaterThan(initialRequests);
  await expect(page.getByText("A correction request was rejected. You can submit a new request.")).toBeVisible();
  const afterFocus = requests;
  await page.clock.fastForward(60_000);
  await expect.poll(() => requests).toBeGreaterThan(afterFocus);
  await attendance.capture(testInfo, "attendance-refreshed-from-server");
});
