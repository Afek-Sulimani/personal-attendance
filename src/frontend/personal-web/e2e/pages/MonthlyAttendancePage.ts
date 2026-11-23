import { expect, type Page } from "@playwright/test";
import { ApplicationPage } from "./ApplicationPage";

export class MonthlyAttendancePage extends ApplicationPage {
  constructor(page: Page) { super(page, "/reports/monthly", "My monthly attendance"); }

  async expectDay(dateLabel: string, status: string, worked?: string): Promise<void> {
    const row = this.page.getByRole("row", { name: new RegExp(dateLabel) });
    await expect(row).toContainText(status);
    if (worked) await expect(row).toContainText(worked);
  }

  async previousMonth(): Promise<void> {
    await this.page.getByRole("button", { name: "Previous payroll period" }).click();
  }
}
