import { test } from "@playwright/test";
import { AttendancePage } from "../pages/AttendancePage";
import { MonthlyAttendancePage } from "../pages/MonthlyAttendancePage";
import { useActor } from "../support/sharedWorkspace";
import { latestPastWorkday } from "../support/workday";

test("EMP-05: latest correction replaces the previous pending request", async ({ page }, testInfo) => {
    await useActor(page, "Employee");
    const attendance = new AttendancePage(page);
    await attendance.open();
    const day = latestPastWorkday(2);
    await attendance.correctMissingDay(day.date, "09", "17");
    await attendance.correctMissingDay(day.date, "09", "18");
    const report = new MonthlyAttendancePage(page);
    await report.open();
    await report.expectDay(day.label, "Awaiting approval", "9h 00m");
    await report.capture(testInfo, "latest-pending-correction");
});
