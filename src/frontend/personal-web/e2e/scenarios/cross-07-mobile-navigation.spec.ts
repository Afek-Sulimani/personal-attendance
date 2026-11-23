import { expect, test } from "@playwright/test";
import { AttendancePage } from "../pages/AttendancePage";
import { useActor } from "../support/sharedWorkspace";

test.use({ viewport: { width: 390, height: 844 } });

test("CROSS-07: mobile menu opens role navigation and closes after choosing a page", async ({ page }, testInfo) => {
  await useActor(page, "Employee");
  const attendance = new AttendancePage(page);
  await attendance.open();
  const menu = page.getByRole("button", { name: "Open menu" });
  await expect(menu).toBeVisible();
  await menu.click();
  await expect(page.getByRole("button", { name: "Close menu" })).toHaveAttribute("aria-expanded", "true");
  await page.getByRole("navigation", { name: "Main navigation" })
    .getByRole("link", { name: "My monthly attendance" }).click();
  await expect(page.getByRole("heading", { name: "My monthly attendance" })).toBeVisible();
  await expect(page.getByRole("button", { name: "Open menu" })).toHaveAttribute("aria-expanded", "false");
  await testInfo.attach("mobile-monthly-attendance", {
    body: await page.screenshot({ fullPage: true }), contentType: "image/png",
  });
});
