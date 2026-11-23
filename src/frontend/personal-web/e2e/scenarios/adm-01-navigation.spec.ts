import { test } from "@playwright/test";
import { AttendancePage } from "../pages/AttendancePage";
import { ProfilePage } from "../pages/ProfilePage";
import { useActor } from "../support/sharedWorkspace";

test("ADM-01: Admin sees its workspace and profile", async ({ page }, testInfo) => {
    const workspace = await useActor(page, "Admin");
    const attendance = new AttendancePage(page);
    await attendance.open();
    await attendance.expectRole("Admin");
    await attendance.expectNavigation([
      "My attendance", "My monthly attendance", "Manage people", "Employee pay details",
      "Company settings", "Workday setup", "Attendance administration", "Team monthly attendance",
      "Review corrections",
    ]);
    await attendance.capture(testInfo, "admin-navigation");
    const profile = new ProfilePage(page);
    await profile.open();
    await profile.expectCompanyAndRole(workspace.companyCode, "Admin");
    await profile.capture(testInfo, "admin-profile");
});
