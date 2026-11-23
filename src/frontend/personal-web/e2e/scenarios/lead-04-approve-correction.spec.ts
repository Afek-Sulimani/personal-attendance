import { expect, test } from "@playwright/test";
import { AttendancePage } from "../pages/AttendancePage";
import { CorrectionReviewPage } from "../pages/CorrectionReviewPage";
import { MonthlyAttendancePage } from "../pages/MonthlyAttendancePage";
import { useActor } from "../support/sharedWorkspace";
import { latestPastWorkday } from "../support/workday";

test("LEAD-04/EMP-07: direct manager approval updates the employee report", async ({ page, browser, baseURL }, testInfo) => {
  const workspace = await useActor(page, "Employee");
  const day = latestPastWorkday(3);
  const attendance = new AttendancePage(page);
  await attendance.open();
  await attendance.correctMissingDay(day.date);

  const leadContext = await browser.newContext({ baseURL, storageState: workspace.sessions.TeamLead });
  try {
    const leadPage = await leadContext.newPage();
    const review = new CorrectionReviewPage(leadPage);
    await review.open();
    await review.capture(testInfo, "manager-review-before-approval");
    await review.approve("E2E Employee", day.date);
  } finally { await leadContext.close(); }

  const report = new MonthlyAttendancePage(page);
  await report.open();
  await report.expectDay(day.label, "Reported", "8h 00m");
  await expect(page.getByRole("row", { name: new RegExp(day.label) })).toContainText("Clock In 09:00");
  await report.capture(testInfo, "employee-report-after-approval");
});
