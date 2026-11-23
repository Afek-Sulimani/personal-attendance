import { expect, type Page } from "@playwright/test";
import { BasePage } from "./BasePage";

export class ActivationPage extends BasePage {
  constructor(page: Page) { super(page, "/activate", "Activate account"); }

  async openLink(activationUrl: string): Promise<void> {
    const link = new URL(activationUrl);
    await this.page.goto(`/activate${link.hash}`);
    await this.ready();
    await expect(this.page.getByRole("region", { name: "Invitation details" })).toBeVisible();
  }

  async activate(password: string): Promise<void> {
    await this.page.getByRole("textbox", { name: "Choose a password" }).fill(password);
    await this.page.getByRole("textbox", { name: "Confirm password" }).fill(password);
    await this.page.getByRole("button", { name: /Activate account/ }).click();
    await expect(this.page.getByRole("heading", { name: "Welcome back" })).toBeVisible();
  }
}
