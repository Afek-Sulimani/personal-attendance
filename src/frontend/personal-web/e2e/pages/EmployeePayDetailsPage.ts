import { expect, type Page } from "@playwright/test";
import { ApplicationPage } from "./ApplicationPage";

export class EmployeePayDetailsPage extends ApplicationPage {
  constructor(page: Page) { super(page, "/admin/employee-profiles", "Employee pay details"); }

  async setHourly(name: string, rate: string): Promise<void> {
    const row = this.page.locator(".employee-compensation-row").filter({ has: this.page.getByRole("heading", { name }) });
    await row.getByRole("combobox", { name: "Pay type" }).selectOption("Hourly");
    await row.getByRole("spinbutton", { name: "Hourly rate (number only)" }).fill(rate);
    await row.getByRole("button", { name: "Save pay details" }).click();
    await expect(this.page.getByRole("status")).toContainText(/saved/i);
  }

  async setGlobal(name: string): Promise<void> {
    const row = this.page.locator(".employee-compensation-row").filter({ has: this.page.getByRole("heading", { name }) });
    await row.getByRole("combobox", { name: "Pay type" }).selectOption("Global");
    await row.getByRole("button", { name: "Save pay details" }).click();
    await expect(this.page.getByRole("status")).toContainText(/saved/i);
  }
}
