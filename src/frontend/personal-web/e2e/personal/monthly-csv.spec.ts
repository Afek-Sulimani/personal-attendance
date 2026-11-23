import { test, expect } from "@playwright/test";
import { readFile } from "node:fs/promises";
const report = {
  employeeMembershipId:"member", employeeName:"=unsafe", employeeNumber:"00001", periodStart:"2026-11-16", periodEnd:"2026-12-15",timeZoneId:"Asia/Jerusalem",isScheduleConfigured:true,scheduledWorkdayCount:1,completedShiftCount:0,openShiftCount:0,missingWorkdayCount:0,pendingCorrectionCount:1,totalWorkedMinutes:480,pendingCorrectionMinutes:480,
  days:[{date:"2026-11-18",isWorkday:true,status:"PendingApproval",shifts:[],pendingCorrection:{clockInAtLocal:"2026-11-18T09:00:00+02:00",clockOutAtLocal:"2026-11-18T17:00:00+02:00",timeZoneId:"Asia/Jerusalem",workedMinutes:480}}]
};
test("Employee exports the selected payroll period with pending hours and safe spreadsheet fields",async({page})=>{
  await page.route("**/api/auth/me",r=>r.fulfill({json:{userId:"user",email:"employee@example.test",displayName:"Employee",companyId:"company",companyCode:"12345",membershipId:"member",employeeProfileId:"profile",employeeNumber:"00001",roles:["Employee"]}}));
  await page.route("**/api/monthly-attendance-reports/mine/current",r=>r.fulfill({json:report}));
  await page.goto("/reports/monthly");
  await expect(page.getByRole("heading",{name:"My monthly attendance"})).toBeVisible();
  const downloadEvent=page.waitForEvent("download");
  await page.getByRole("button",{name:"Download CSV"}).click();
  const download=await downloadEvent;
  expect(download.suggestedFilename()).toBe("attendance-2026-11-16-2026-12-15.csv");
  const content=await readFile((await download.path())!,"utf8");
  expect(content).toContain("Awaiting approval");
  expect(content).toContain("Asia/Jerusalem");
  expect(content).toContain("480");
  expect(content).toContain("'=unsafe");
});
