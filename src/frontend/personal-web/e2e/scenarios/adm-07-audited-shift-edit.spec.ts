import { test } from "@playwright/test";
import { AdminAttendancePage } from "../pages/AdminAttendancePage";
import { AttendancePage } from "../pages/AttendancePage";
import { CorrectionReviewPage } from "../pages/CorrectionReviewPage";
import { useActor } from "../support/sharedWorkspace";
import { latestPastWorkday } from "../support/workday";

test("ADM-07–08: Admin edits only an existing shift with an audit reason", async ({ page, browser, baseURL }, testInfo) => {
  const workspace = await useActor(page, "Employee");
  const day = latestPastWorkday(5);
  const attendance = new AttendancePage(page);
  await attendance.open();
  await attendance.correctMissingDay(day.date);

  const leadContext = await browser.newContext({ baseURL, storageState: workspace.sessions.TeamLead });
  try {
    const leadPage = await leadContext.newPage();
    const review = new CorrectionReviewPage(leadPage);
    await review.open();
    await review.approve("E2E Employee", day.date);
  } finally { await leadContext.close(); }

  const adminContext = await browser.newContext({ baseURL, storageState: workspace.sessions.Admin });
  try {
    const adminPage = await adminContext.newPage();
    const admin = new AdminAttendancePage(adminPage);
    await admin.open();
    await admin.selectEmployee("E2E Employee");
    await admin.updateExistingShift(day.date, "E2E verified shift adjustment");
    await admin.capture(testInfo, "audited-shift-edit");
  } finally { await adminContext.close(); }
});
