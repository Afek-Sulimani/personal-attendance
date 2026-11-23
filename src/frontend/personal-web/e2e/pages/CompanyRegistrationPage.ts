import { expect, type Page } from "@playwright/test";
import { BasePage } from "./BasePage";

export class CompanyRegistrationPage extends BasePage {
  constructor(page: Page) { super(page, "/register-company", "Register a company"); }

  async register(name: string, representative: string, email: string): Promise<{ companyCode: string; activationUrl: string }> {
    await this.page.getByRole("textbox", { name: "Company name" }).fill(name);
    await this.page.getByRole("textbox", { name: "Company representative name" }).fill(representative);
    await this.page.getByRole("textbox", { name: "Company representative email" }).fill(email);
    await this.page.getByRole("button", { name: "Create company" }).click();
    const result = this.page.getByRole("region", { name: "Company registration result" });
    await expect(this.page.getByRole("heading", { name: "Company created" })).toBeVisible();
    const companyCode = (await result.locator(".registration-value-content").first().innerText()).trim();
    const activationUrl = await result.getByRole("link", { name: /Temporary link to activate/ }).getAttribute("href");
    if (!activationUrl) throw new Error("Registration did not provide an activation link.");
    return { companyCode, activationUrl };
  }
}
