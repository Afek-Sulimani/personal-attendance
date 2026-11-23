import { expect, test } from "@playwright/test";
import { AttendancePage } from "../pages/AttendancePage";
import { useActor } from "../support/sharedWorkspace";

test("LEAD-06: prior-period pending correction highlights navigation and employee row", async ({ page }, testInfo) => {
  await useActor(page, "TeamLead");
  await page.route("**/api/attendance/corrections/pending", route => route.fulfill({
    status: 200,
    contentType: "application/json",
    body: JSON.stringify([{
      requestId: "11111111-1111-4111-8111-111111111111",
      employeeName: "E2E Employee",
      employeeNumber: "00005",
      workDate: "2026-11-15",
      timeZoneId: "Asia/Jerusalem",
      requestedClockInAtLocal: "2026-11-15T09:00:00+02:00",
      requestedClockOutAtLocal: "2026-11-15T17:00:00+02:00",
      submittedAtLocal: "2026-11-15T18:00:00+02:00",
      status: "PendingApproval",
      rejectionReason: null,
      isAttentionRequired: true,
    }]),
  }));

  const attendance = new AttendancePage(page);
  await attendance.open();
  const corrections = page.getByRole("link", { name: /Review corrections/ });
  await expect(corrections.getByLabel("1 corrections need attention")).toBeVisible();
  await corrections.click();
  const employee = page.getByRole("region", { name: "Pending corrections for E2E Employee" });
  await expect(employee.getByText("1 awaiting approval")).toBeVisible();
  await expect(employee.getByRole("row", { name: /Nov 15, 2026/ })).toContainText("15 Nov, 09:00");
  await testInfo.attach("cutoff-attention-table", {
    body: await page.screenshot({ fullPage: true }), contentType: "image/png",
  });
});
