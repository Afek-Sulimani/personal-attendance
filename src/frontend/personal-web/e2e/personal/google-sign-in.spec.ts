import {test,expect} from "@playwright/test";
for (const enabled of [true,false]) {
 test(`Google option follows server configuration: ${enabled}`, async({page})=>{
  await page.route("**/api/auth/me",r=>r.fulfill({status:401,json:{}}));
  await page.route("**/api/auth/google/status",r=>r.fulfill({json:{enabled}}));
  await page.goto("/login");
  const google=page.getByRole("button",{name:"Continue with Google"});
  if (!enabled) {await expect(google).toHaveCount(0);return;}
  await expect(google).toBeDisabled();
  await page.getByLabel("Company name or ID").fill("12345");
  await expect(google).toBeEnabled();
  await page.route("**/api/auth/google/start?*",r=>r.fulfill({contentType:"text/html",body:"<p>OAuth handoff</p>"}));
  await google.click();
  await expect(page).toHaveURL(/companyIdentifier=12345/);
 });
}
