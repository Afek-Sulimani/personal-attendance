import { expect, type Page } from "@playwright/test";
import { ApplicationPage } from "./ApplicationPage";

export class TeamMonthlyAttendancePage extends ApplicationPage {
  constructor(page: Page) { super(page, "/team/monthly-reports", "Monthly attendance"); }

  async selectEmployee(name: string): Promise<void> {
    const option = this.page.getByRole("option", { name: new RegExp(name) });
    const value = await option.getAttribute("value");
    if (!value) throw new Error(`Employee option ${name} is unavailable.`);
    await this.page.getByRole("combobox", { name: "Employee" }).selectOption(value);
    await expect(this.page.getByRole("heading", { name: "No employees are available to review" })).toHaveCount(0);
  }
}
