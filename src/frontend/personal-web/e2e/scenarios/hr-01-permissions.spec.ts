import { expect, test } from "@playwright/test";
import { EmployeeInvitationsPage } from "../pages/EmployeeInvitationsPage";
import { useActor } from "../support/sharedWorkspace";

test("HR-01–02: HR can invite staff but cannot assign Admin", async ({ page }, testInfo) => {
    await useActor(page, "HR");
    const people = new EmployeeInvitationsPage(page);
    await people.open();
    await people.expectRole("HR");
    await people.expectNoNavigation(["Company settings", "Workday setup", "Attendance administration"]);
    await expect(page.getByRole("checkbox", { name: "Payroll" })).toBeVisible();
    await expect(page.getByRole("checkbox", { name: "Admin" })).toHaveCount(0);
    await people.capture(testInfo, "hr-permissions");
});
