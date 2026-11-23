import { expect, test } from "@playwright/test";
import { WorkdaySetupPage } from "../pages/WorkdaySetupPage";
import { useActor } from "../support/sharedWorkspace";

test("PAY-03: Payroll saves a monthly workday override for its own profile", async ({ page }, testInfo) => {
  await useActor(page, "PayrollRepresentative");
  const workdays = new WorkdaySetupPage(page);
  await workdays.open();
  await workdays.selectEmployee("E2E Payroll");
  await page.getByRole("checkbox", { name: "Friday" }).check();
  await workdays.save();
  await page.reload();
  await workdays.ready();
  await workdays.selectEmployee("E2E Payroll");
  await expect(page.getByRole("checkbox", { name: "Friday" })).toBeChecked();
  await workdays.capture(testInfo, "payroll-workday-override");
});
