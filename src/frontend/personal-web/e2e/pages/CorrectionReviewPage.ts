import { expect, type Page } from "@playwright/test";
import { ApplicationPage } from "./ApplicationPage";

export class CorrectionReviewPage extends ApplicationPage {
  constructor(page: Page) { super(page, "/team/attendance-corrections", "Attendance corrections"); }

  requestFor(name: string, date: string) {
    const label = new Intl.DateTimeFormat("en-US", { dateStyle: "medium", timeZone: "UTC" })
      .format(new Date(`${date}T12:00:00Z`));
    return this.page.getByRole("region", { name: `Requests for ${name}` })
      .getByRole("row", { name: new RegExp(label) });
  }

  async approve(name: string, date: string): Promise<void> {
    const request = this.requestFor(name, date);
    await request.getByRole("button", { name: "Approve" }).click();
    await expect(this.page.getByRole("status").filter({ hasText: "Correction approved" })).toBeVisible();
  }

  async reject(name: string, date: string, reason: string): Promise<void> {
    const request = this.requestFor(name, date);
    await request.getByRole("button", { name: "Reject", exact: true }).click();
    await request.getByRole("textbox", { name: "Reason for rejection" }).fill(reason);
    await request.getByRole("button", { name: "Reject with reason" }).click();
    await expect(this.page.getByRole("status").filter({ hasText: "Correction rejected" })).toBeVisible();
  }
}
