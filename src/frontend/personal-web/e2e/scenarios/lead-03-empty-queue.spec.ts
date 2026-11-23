import { expect, test } from "@playwright/test";
import { CorrectionReviewPage } from "../pages/CorrectionReviewPage";
import { LoginPage } from "../pages/LoginPage";
import { TEST_PASSWORD, TestWorkspace } from "../support/TestWorkspace";

test("LEAD-03: empty review queue should be visible to a direct manager", async ({ page, baseURL }, testInfo) => {
  const workspace = await TestWorkspace.create(baseURL!);
  try {
    const lead = await workspace.invite("TeamLead");
    await workspace.invite("Employee", "TeamLead");
    const login = new LoginPage(page);
    await login.open();
    await login.signIn(workspace.companyCode, lead.email, TEST_PASSWORD);
    const review = new CorrectionReviewPage(page);
    await review.open();
    await expect(page.getByRole("heading", { name: "No pending correction requests" })).toBeVisible();
    await review.capture(testInfo, "empty-review-queue");
  } finally { await workspace.dispose(); }
});
