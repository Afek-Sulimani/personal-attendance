import { expect, test } from "@playwright/test";
import { ActivationPage } from "../pages/ActivationPage";
import { AttendancePage } from "../pages/AttendancePage";
import { CompanyRegistrationPage } from "../pages/CompanyRegistrationPage";
import { LoginPage } from "../pages/LoginPage";
import { TEST_PASSWORD } from "../support/TestWorkspace";

test("SETUP-01–03: company registration, first Admin activation, sign in", async ({ page }, testInfo) => {
  const id = `${Date.now()}-${Math.random().toString(36).slice(2, 7)}`;
  const email = `admin-onboarding-${id}@example.test`;
  const registration = new CompanyRegistrationPage(page);
  await registration.open();
  const result = await registration.register(`E2E Onboarding ${id}`, "E2E Admin", email);
  expect(result.companyCode).toMatch(/^\d{5}$/);
  await registration.capture(testInfo, "company-created");

  const activation = new ActivationPage(page);
  await activation.openLink(result.activationUrl);
  await activation.capture(testInfo, "activation-form");
  await activation.activate(TEST_PASSWORD);

  const login = new LoginPage(page);
  await login.signIn(result.companyCode, email, TEST_PASSWORD);
  const attendance = new AttendancePage(page);
  await attendance.expectRole("Admin");
  await attendance.capture(testInfo, "first-admin-workspace");

  await attendance.signOut();
  const link = new URL(result.activationUrl);
  await page.goto(`/activate${link.hash}`);
  await expect(page.getByRole("alert")).toContainText(/invalid|expired|used/i);
});
