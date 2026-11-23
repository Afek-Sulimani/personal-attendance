import { readFileSync } from "node:fs";
import { resolve } from "node:path";
import type { Page } from "@playwright/test";
import type { SharedWorkspace, TestActor } from "./TestWorkspace";

const statePath = resolve("test-results/shared-workspace.json");

export function sharedWorkspace(): SharedWorkspace {
  return JSON.parse(readFileSync(statePath, "utf8")) as SharedWorkspace;
}

export async function useActor(page: Page, role: TestActor["role"]): Promise<SharedWorkspace> {
  const workspace = sharedWorkspace();
  await page.context().addCookies(workspace.sessions[role].cookies);
  return workspace;
}
