import { expect, test } from "@playwright/test";
import { LoginPage } from "../pages/LoginPage";
import { sharedWorkspace } from "../support/sharedWorkspace";

test("CROSS-01: invalid password does not open a session or disclose which field is wrong", async ({ page }, testInfo) => {
  const workspace = sharedWorkspace();
  const login = new LoginPage(page);
  await login.open();
  await page.getByRole("textbox", { name: "Company name or ID" }).fill(workspace.companyCode);
  await page.getByRole("textbox", { name: "Email" }).fill(workspace.actors.Employee.email);
  await page.getByRole("textbox", { name: "Password" }).fill("WrongPassword!42");
  await page.getByRole("button", { name: "Sign in", exact: true }).click();
  await expect(page.getByRole("alert")).toContainText("Invalid company name or ID, email, or password.");
  await expect(page.getByRole("heading", { name: "Welcome back" })).toBeVisible();
  await login.capture(testInfo, "invalid-login");
});
