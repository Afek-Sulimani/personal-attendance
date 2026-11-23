import { expect, test } from "@playwright/test";
import { AttendancePage } from "../pages/AttendancePage";
import { CorrectionReviewPage } from "../pages/CorrectionReviewPage";
import { useActor } from "../support/sharedWorkspace";
import { latestPastWorkday } from "../support/workday";

test("LEAD-05: manager cannot reject without a reason and can reject with one", async ({ page, browser, baseURL }, testInfo) => {
  const workspace = await useActor(page, "Employee");
  const day = latestPastWorkday(4);
  const attendance = new AttendancePage(page);
  await attendance.open();
  await attendance.correctMissingDay(day.date);

  const leadContext = await browser.newContext({ baseURL, storageState: workspace.sessions.TeamLead });
  try {
    const leadPage = await leadContext.newPage();
    const review = new CorrectionReviewPage(leadPage);
    await review.open();
    await expect(review.requestFor("E2E Employee", day.date).getByRole("button", { name: "Reject", exact: true })).toBeEnabled();
    await review.reject("E2E Employee", day.date, "Please verify the reported end time.");
    await review.capture(testInfo, "manager-rejected-with-reason");
  } finally { await leadContext.close(); }
});
