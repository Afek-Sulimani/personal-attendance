import { expect, test } from "@playwright/test";
import { AttendancePage } from "../pages/AttendancePage";
import { useActor } from "../support/sharedWorkspace";

test("EMP-14: server date rollover removes live actions from yesterday", async ({ page }, testInfo) => {
  await useActor(page, "Employee");
  let crossedMidnight = false;
  await page.route("**/api/attendance/calendar*", async route => {
    const response = await route.fetch();
    const calendar = await response.json();
    if (crossedMidnight) {
      const next = new Date(`${calendar.todayLocalDate}T12:00:00Z`);
      next.setUTCDate(next.getUTCDate() + 1);
      calendar.todayLocalDate = next.toISOString().slice(0, 10);
    }
    await route.fulfill({ response, json: calendar });
  });
  const attendance = new AttendancePage(page);
  await attendance.open();
  await expect(page.getByRole("button", { name: "Clock In now" })).toBeVisible();
  crossedMidnight = true;
  const refreshed = page.waitForResponse(response => response.url().includes("/api/attendance/calendar"));
  await page.evaluate(() => document.dispatchEvent(new Event("visibilitychange")));
  await refreshed;
  await expect(page.getByRole("button", { name: "Clock In now" })).toHaveCount(0);
  await expect(page.getByRole("button", { name: "Clock Out now" })).toHaveCount(0);
  await attendance.capture(testInfo, "server-midnight-yesterday-actions-hidden");
});
