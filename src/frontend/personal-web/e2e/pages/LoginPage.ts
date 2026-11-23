import { expect, type Page } from "@playwright/test";
import { BasePage } from "./BasePage";

export class LoginPage extends BasePage {
  constructor(page: Page) { super(page, "/login", "Welcome back"); }

  async signIn(company: string, email: string, password: string): Promise<void> {
    await this.page.getByRole("textbox", { name: "Company name or ID" }).fill(company);
    await this.page.getByRole("textbox", { name: "Email" }).fill(email);
    await this.page.getByRole("textbox", { name: "Password" }).fill(password);
    await this.page.getByRole("button", { name: "Sign in", exact: true }).click();
    await expect(this.page.getByRole("heading", { name: "My attendance", exact: true })).toBeVisible();
  }
}
