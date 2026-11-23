import { expect, type Page } from "@playwright/test";
import { ApplicationPage } from "./ApplicationPage";

export class EmployeeInvitationsPage extends ApplicationPage {
  constructor(page: Page) { super(page, "/admin/people", "Add a company member"); }

  async invite(name: string, email: string, role: string): Promise<string> {
    await this.page.getByRole("textbox", { name: "Employee name" }).fill(name);
    await this.page.getByRole("textbox", { name: "Employee email" }).fill(email);
    if (role !== "Employee") {
      await this.page.getByRole("checkbox", { name: "Employee", exact: true }).uncheck();
      await this.page.getByRole("checkbox", { name: role, exact: true }).check();
    }
    await this.page.getByRole("button", { name: "Create activation link" }).click();
    await expect(this.page.getByRole("heading", { name: "Invitation created" })).toBeVisible();
    const url = await this.page.getByRole("link", { name: /Temporary link for activating/ }).getAttribute("href");
    if (!url) throw new Error("Invitation did not provide an activation link.");
    return url;
  }

  async addAnother(): Promise<void> {
    await this.page.getByRole("button", { name: "Add another employee" }).click();
    await this.ready();
  }
}
