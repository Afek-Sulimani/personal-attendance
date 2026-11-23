import { test } from "@playwright/test";
import { AttendancePage } from "../pages/AttendancePage";
import { ProfilePage } from "../pages/ProfilePage";
import { useActor } from "../support/sharedWorkspace";

test("EMP-01: Employee sees only personal navigation and profile", async ({ page }, testInfo) => {
    const workspace = await useActor(page, "Employee");
    const attendance = new AttendancePage(page);
    await attendance.open();
    await attendance.expectRole("Employee");
    await attendance.expectNavigation(["My attendance", "My monthly attendance"]);
    await attendance.expectNoNavigation([
      "Manage people", "Employee pay details", "Company settings", "Workday setup",
      "Attendance administration", "Review corrections", "Team monthly attendance",
    ]);
    const profile = new ProfilePage(page);
    await profile.open();
    await profile.expectCompanyAndRole(workspace.companyCode, "Employee");
    await profile.expectEmail(workspace.actors.Employee.email);
    await profile.capture(testInfo, "employee-profile");
});
