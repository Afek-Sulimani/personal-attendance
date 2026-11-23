import { expect, test } from "@playwright/test";
import { useActor } from "../support/sharedWorkspace";

test("EMP-10: Employee cannot access Admin attendance through a direct URL", async ({ page }, testInfo) => {
  await useActor(page, "Employee");
  const response = await page.goto("/admin/attendance");
  expect(response?.status()).toBe(200); // SPA shell loads; its protected API must still reject.
  await expect(page.getByRole("heading", { name: "Attendance records" })).toHaveCount(0);
  await expect(page.getByRole("status")).toHaveText("You do not have access to attendance administration.");
  await testInfo.attach("employee-admin-access-denied", {
    body: await page.screenshot({ fullPage: true }), contentType: "image/png",
  });
});
