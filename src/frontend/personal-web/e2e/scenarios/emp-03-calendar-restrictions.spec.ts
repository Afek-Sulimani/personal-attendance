import { expect, test } from "@playwright/test";
import { AttendancePage } from "../pages/AttendancePage";
import { useActor } from "../support/sharedWorkspace";

test("EMP-03: future and non-workday dates do not expose live punches", async ({ page }, testInfo) => {
    await useActor(page, "Employee");
    const attendance = new AttendancePage(page);
    await attendance.open();
    await attendance.selectDate((await page.getByRole("button", { name: /Future date/ }).first().getAttribute("aria-label"))!.slice(0, 10));
    await expect(page.getByRole("button", { name: "Clock In now" })).toHaveCount(0);
    await expect(page.getByRole("button", { name: "Clock Out now" })).toHaveCount(0);
    await attendance.capture(testInfo, "future-date-no-punch");
});
