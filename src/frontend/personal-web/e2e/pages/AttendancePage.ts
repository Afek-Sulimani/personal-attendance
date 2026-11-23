import { expect, type Page } from "@playwright/test";
import { ApplicationPage } from "./ApplicationPage";

export class AttendancePage extends ApplicationPage {
  constructor(page: Page) { super(page, "/attendance", "My attendance"); }

  async selectDate(date: string): Promise<void> {
    await this.page.getByRole("button", { name: new RegExp(`^${date}:`) }).click();
  }

  async correctMissingDay(date: string, inHour = "09", outHour = "17"): Promise<void> {
    await this.selectDate(date);
    await this.page.getByRole("combobox", { name: /Clock in .* hour/ }).selectOption(inHour);
    await this.page.getByRole("combobox", { name: /Clock out .* hour/ }).selectOption(outHour);
    const submitted = this.page.waitForResponse(response =>
      response.url().endsWith("/api/attendance/corrections") && response.request().method() === "POST",
    );
    await this.page.getByRole("button", { name: /Send for manager approval|Replace pending request/ }).click();
    const response = await submitted;
    expect(response.status()).toBe(202);
    await expect(this.page.getByText("A correction request is waiting for manager approval.")).toBeVisible();
  }

  async clockIn(): Promise<void> {
    await this.page.getByRole("button", { name: "Clock In now" }).click();
    await expect(this.page.getByRole("button", { name: "Clock Out now" })).toBeEnabled();
  }

  async clockOut(): Promise<void> {
    await this.page.getByRole("button", { name: "Clock Out now" }).click();
    await expect(this.page.getByRole("button", { name: "Clock In now" })).toBeDisabled();
  }
}
