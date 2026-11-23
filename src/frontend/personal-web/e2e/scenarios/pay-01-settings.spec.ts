import { test } from "@playwright/test";
import { CompanySettingsPage } from "../pages/CompanySettingsPage";
import { LoginPage } from "../pages/LoginPage";
import { TEST_PASSWORD, TestWorkspace } from "../support/TestWorkspace";

test("PAY-01–02: Payroll configures company time and workdays", async ({ page, baseURL }, testInfo) => {
  const workspace = await TestWorkspace.create(baseURL!);
  try {
    const payroll = await workspace.invite("PayrollRepresentative");
    const login = new LoginPage(page);
    await login.open();
    await login.signIn(workspace.companyCode, payroll.email, TEST_PASSWORD);
    const settings = new CompanySettingsPage(page);
    await settings.open();
    await settings.expectRole("Payroll");
    await settings.expectNoNavigation(["Attendance administration"]);
    await settings.saveDefaults();
    await settings.capture(testInfo, "payroll-settings-saved");
  } finally { await workspace.dispose(); }
});
