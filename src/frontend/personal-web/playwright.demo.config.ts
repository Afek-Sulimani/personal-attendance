import { defineConfig } from "@playwright/test";
export default defineConfig({testDir:"./e2e/demo",testMatch:"*.spec.ts",workers:1,timeout:180_000,expect:{timeout:10000},use:{baseURL:"http://127.0.0.1:5178"},outputDir:"test-results/demo",reporter:"list"});
