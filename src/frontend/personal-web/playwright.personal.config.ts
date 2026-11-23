import { defineConfig } from "@playwright/test";
export default defineConfig({
  testDir: "./e2e/personal", workers: 1,
  use: { baseURL: "http://127.0.0.1:5177", screenshot: "on", trace: "retain-on-failure" },
  webServer: {command: "npm run dev -- --port 5177 --strictPort", url: "http://127.0.0.1:5177", reuseExistingServer: false},
});
