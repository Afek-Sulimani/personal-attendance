import { expect, type Page } from "@playwright/test";
import { ApplicationPage } from "./ApplicationPage";

export class WorkdaySetupPage extends ApplicationPage {
  constructor(page: Page) { super(page, "/payroll/work-days", "Workday setup"); }

  async selectEmployee(name: string): Promise<void> {
    await this.page.getByRole("searchbox", { name: "Search employees" }).fill(name);
    const value = await this.page.getByRole("option", { name: new RegExp(name) }).getAttribute("value");
    if (!value) throw new Error(`Employee option ${name} is unavailable.`);
    await this.page.getByRole("combobox", { name: "Employee", exact: true }).selectOption(value);
  }

  async save(): Promise<void> {
    await this.page.getByRole("button", { name: "Save workdays" }).click();
    await expect(this.page.getByRole("status")).toContainText(/saved/i);
  }
}
