import { expect, test } from "@playwright/test";
import { LoginPage } from "../pages/LoginPage";

test("CROSS-08: pending sign in shows spinner, prevents duplicates, and recovers on error", async ({ page }, testInfo) => {
  const login = new LoginPage(page);
  await login.open();
  let release!: () => void;
  const gate = new Promise<void>(resolve => { release = resolve; });
  let submissions = 0;
  await page.route("**/api/auth/login", async route => {
    submissions++;
    await gate;
    await route.fulfill({ status: 503, contentType: "application/problem+json", body: JSON.stringify({ title: "Service unavailable", detail: "Please try again." }) });
  });
  await page.getByRole("textbox", { name: "Company name or ID" }).fill("12345");
  await page.getByRole("textbox", { name: "Email" }).fill("loading@example.test");
  await page.getByRole("textbox", { name: "Password" }).fill("FlowTest!2026");
  await page.getByRole("button", { name: "Sign in", exact: true }).click();
  const pending = page.getByRole("button", { name: "Signing in…" });
  await expect(pending).toBeDisabled();
  await expect(pending).toHaveAttribute("aria-busy", "true");
  await expect(pending.locator(".loading-spinner")).toBeVisible();
  await expect(pending.locator(".loading-spinner")).toHaveAttribute("aria-hidden", "true");
  await expect.poll(() => submissions).toBe(1);
  await login.capture(testInfo, "sign-in-pending-spinner");
  release();
  await expect(page.getByRole("alert")).toBeVisible();
  await expect(page.getByRole("button", { name: "Sign in", exact: true })).toBeEnabled();
  await expect(page.locator(".loading-spinner")).toHaveCount(0);
});

test("CROSS-08: reduced motion keeps loading feedback static", async ({ page }) => {
  await page.emulateMedia({ reducedMotion: "reduce" });
  let release!: () => void;
  const gate = new Promise<void>(resolve => { release = resolve; });
  await page.route("**/api/auth/me", async route => {
    await gate;
    await route.fulfill({ status: 401, body: "" });
  });
  await page.goto("/login");
  const spinner = page.locator(".loading-spinner");
  await expect(spinner).toBeVisible();
  await expect(spinner).toHaveCSS("animation-name", "none");
  release();
  await expect(spinner).toHaveCount(0);
});
