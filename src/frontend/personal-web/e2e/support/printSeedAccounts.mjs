import { readFileSync } from "node:fs";
import { resolve } from "node:path";

const { companyCode, companyName, actors } = JSON.parse(
  readFileSync(resolve("test-results/shared-workspace.json"), "utf8"),
);

const testPassword = process.env.E2E_TEST_PASSWORD ?? "FlowTest!2026";
const loginUrl = new URL("/login", process.env.E2E_BASE_URL ?? "http://127.0.0.1:5175").toString();

console.log(`Company: ${companyName} (${companyCode})`);
console.log(`Login: ${loginUrl}`);
console.table(Object.values(actors).map(({ displayName, role, email }) => ({ displayName, role, email })));
console.log(`Shared test password: ${testPassword}`);
console.log("These are local-only test accounts. Do not reuse this password for real accounts.");
