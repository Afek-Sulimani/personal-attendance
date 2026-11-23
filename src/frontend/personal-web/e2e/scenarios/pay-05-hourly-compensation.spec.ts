import { expect, test } from "@playwright/test";
import { EmployeePayDetailsPage } from "../pages/EmployeePayDetailsPage";
import { ProfilePage } from "../pages/ProfilePage";
import { useActor } from "../support/sharedWorkspace";

test("PAY-05: Payroll sets an hourly rate and Employee sees it", async ({ page, browser, baseURL }, testInfo) => {
  const workspace = await useActor(page, "PayrollRepresentative");
  const pay = new EmployeePayDetailsPage(page);
  await pay.open();
  await pay.setHourly("E2E Employee", "42.50");
  await pay.capture(testInfo, "hourly-pay-saved");

  const employeeContext = await browser.newContext({ baseURL, storageState: workspace.sessions.Employee });
  try {
    const employeePage = await employeeContext.newPage();
    const profile = new ProfilePage(employeePage);
    await profile.open();
    await expect(employeePage.getByRole("region", { name: "Employment details" })).toContainText("42.50");
    await profile.capture(testInfo, "employee-sees-hourly-pay");
  } finally { await employeeContext.close(); }
});
