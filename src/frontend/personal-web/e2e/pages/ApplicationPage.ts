import { expect, type Page } from "@playwright/test";
import { BasePage } from "./BasePage";

export abstract class ApplicationPage extends BasePage {
  protected constructor(page: Page, path: string, heading: string) {
    super(page, path, heading);
  }

  async expectNavigation(labels: string[]): Promise<void> {
    const navigation = this.page.getByRole("navigation", { name: "Main navigation" });
    for (const label of labels) {
      await expect(navigation.getByRole("link", { name: label, exact: true })).toBeVisible();
    }
  }

  async expectNoNavigation(labels: string[]): Promise<void> {
    const navigation = this.page.getByRole("navigation", { name: "Main navigation" });
    for (const label of labels) {
      await expect(navigation.getByRole("link", { name: label, exact: true })).toHaveCount(0);
    }
  }

  async expectRole(role: string): Promise<void> {
    await expect(this.page.getByLabel("Your roles").getByText(role, { exact: true })).toBeVisible();
  }

  async signOut(): Promise<void> {
    await this.page.getByRole("button", { name: "Sign out" }).click();
    await expect(this.page.getByRole("heading", { name: "Welcome back" })).toBeVisible();
  }
}
