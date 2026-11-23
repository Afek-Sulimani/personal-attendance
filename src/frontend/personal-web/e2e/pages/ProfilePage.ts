import { expect, type Page } from "@playwright/test";
import { ApplicationPage } from "./ApplicationPage";

export class ProfilePage extends ApplicationPage {
  constructor(page: Page) { super(page, "/profile", "My profile"); }

  async expectCompanyAndRole(companyCode: string, role: string): Promise<void> {
    const details = this.page.getByRole("region", { name: "Employment details" });
    await expect(details).toContainText(companyCode);
    await expect(details).toContainText(role);
  }

  async expectEmail(email: string): Promise<void> {
    await expect(this.page.getByRole("region", { name: "Sign-in information" })).toContainText(email);
  }
}
