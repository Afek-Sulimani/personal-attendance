import { test } from "@playwright/test";
import { AttendancePage } from "../pages/AttendancePage";
import { TeamMonthlyAttendancePage } from "../pages/TeamMonthlyAttendancePage";
import { useActor } from "../support/sharedWorkspace";

test("LEAD-01–02: Team lead sees direct report, not company administration", async ({ page }, testInfo) => {
    await useActor(page, "TeamLead");
    const attendance = new AttendancePage(page);
    await attendance.open();
    await attendance.expectRole("Team lead");
    await attendance.expectNavigation(["Review corrections", "Team monthly attendance"]);
    await attendance.expectNoNavigation(["Manage people", "Employee pay details", "Company settings", "Workday setup"]);
    const team = new TeamMonthlyAttendancePage(page);
    await team.open();
    await team.selectEmployee("E2E Employee");
    await team.capture(testInfo, "team-lead-monthly-report");
});
