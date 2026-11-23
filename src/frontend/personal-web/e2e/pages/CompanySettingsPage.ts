import { expect, type Page } from "@playwright/test";
import { ApplicationPage } from "./ApplicationPage";

export class CompanySettingsPage extends ApplicationPage {
  constructor(page: Page) { super(page, "/payroll/company-settings", "Payroll and work settings"); }

  async saveDefaults(): Promise<void> {
    await this.page.getByRole("spinbutton", { name: /Payroll period starts on day/ }).fill("16");
    await this.page.getByRole("combobox", { name: /Company time zone/ }).fill("Asia/Jerusalem");
    await this.page.getByLabel("Standard work start time").fill("09:00");
    await this.page.getByLabel("Standard work end time").fill("17:00");
    for (const day of ["Sunday", "Monday", "Tuesday", "Wednesday", "Thursday", "Saturday"]) {
      await this.page.getByRole("checkbox", { name: day }).check();
    }
    await this.page.getByRole("button", { name: /Complete company setup|Save company settings/ }).click();
    await expect(this.page.getByRole("status")).toContainText("Company payroll settings saved.");
  }
}
