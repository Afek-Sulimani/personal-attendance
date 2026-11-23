import { defineConfig, devices } from "@playwright/test";

const baseURL = process.env.E2E_BASE_URL ?? "http://127.0.0.1:5175";
const target = new URL(baseURL);
if (!["localhost", "127.0.0.1"].includes(target.hostname)) {
  throw new Error("Playwright E2E tests create accounts and require a local-only E2E_BASE_URL.");
}

export default defineConfig({
  testDir: "./e2e/scenarios",
  globalSetup: "./e2e/support/globalSetup.ts",
  fullyParallel: false,
  workers: 1,
  timeout: 45_000,
  expect: { timeout: 8_000 },
  reporter: [["list"], ["html", { open: "never" }]],
  use: {
    baseURL,
    ...devices["Desktop Chrome"],
    screenshot: "only-on-failure",
    trace: "retain-on-failure",
  },
  outputDir: "test-results",
});
