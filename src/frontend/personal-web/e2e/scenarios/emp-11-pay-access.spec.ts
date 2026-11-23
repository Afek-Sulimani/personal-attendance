import { expect, test } from "@playwright/test";
import { useActor } from "../support/sharedWorkspace";

test("EMP-11: Employee cannot open company pay management", async ({ page }, testInfo) => {
  await useActor(page, "Employee");
  await page.goto("/admin/employee-profiles");
  await expect(page.getByRole("heading", { name: "Employee pay details" })).toHaveCount(0);
  await expect(page.getByRole("status")).toHaveText("You do not have access to employee pay details.");
  await testInfo.attach("employee-pay-access-denied", {
    body: await page.screenshot({ fullPage: true }), contentType: "image/png",
  });
});
