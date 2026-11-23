import { mkdir, writeFile } from "node:fs/promises";
import { resolve } from "node:path";
import type { FullConfig } from "@playwright/test";
import { TestWorkspace } from "./TestWorkspace";

export default async function globalSetup(config: FullConfig): Promise<void> {
  const baseURL = config.projects[0]?.use.baseURL;
  if (!baseURL) throw new Error("E2E_BASE_URL is required.");
  const workspace = await TestWorkspace.create(baseURL);
  try {
    await workspace.invite("PayrollRepresentative");
    await workspace.invite("HR");
    await workspace.invite("TeamLead");
    await workspace.invite("Employee", "TeamLead");
    await workspace.configureCompany();
    await mkdir(resolve("test-results"), { recursive: true });
    await writeFile(resolve("test-results/shared-workspace.json"), JSON.stringify(await workspace.snapshot()), { mode: 0o600 });
  } finally {
    await workspace.dispose();
  }
}
