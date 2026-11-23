import { expect, test } from "@playwright/test";
import { AttendancePage } from "../pages/AttendancePage";
import { useActor } from "../support/sharedWorkspace";

test("EMP-02: live Clock In and Clock Out change the available actions", async ({ page }, testInfo) => {
  await useActor(page, "Employee");
  const attendance = new AttendancePage(page);
  await attendance.open();
  const clockIn = page.getByRole("button", { name: "Clock In now" });
  test.skip(!(await clockIn.isEnabled()), "Today is not a scheduled workday or a shift already exists.");
  await expect(page.getByRole("button", { name: "Clock Out now" })).toBeDisabled();
  await attendance.clockIn();
  await attendance.capture(testInfo, "open-shift");
  await attendance.clockOut();
  await attendance.capture(testInfo, "completed-shift");
});
