import { expect, test } from "@playwright/test";
import { EmployeeInvitationsPage } from "../pages/EmployeeInvitationsPage";
import { useActor } from "../support/sharedWorkspace";

test("ADM-02: Admin invites Payroll with one role and a single-use result", async ({ page }, testInfo) => {
    const workspace = await useActor(page, "Admin");
    const people = new EmployeeInvitationsPage(page);
    await people.open();
    await people.invite("E2E Extra Payroll", `extra-payroll-${workspace.suffix}@example.test`, "Payroll");
    await expect(page.getByRole("status", { name: /Share the activation link/ })).toContainText("Payroll");
    await expect(page.getByRole("button", { name: "Create activation link" })).toHaveCount(0);
    await people.capture(testInfo, "payroll-invitation-created");
    await people.addAnother();
    await expect(page.getByRole("textbox", { name: "Employee name" })).toBeEmpty();
});
