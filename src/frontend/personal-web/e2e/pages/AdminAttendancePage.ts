import { expect, type Page } from "@playwright/test";
import { ApplicationPage } from "./ApplicationPage";

export class AdminAttendancePage extends ApplicationPage {
  constructor(page: Page) { super(page, "/admin/attendance", "Attendance records"); }

  async selectEmployee(name: string): Promise<void> {
    const value = await this.page.getByRole("option", { name: new RegExp(name) }).getAttribute("value");
    if (!value) throw new Error(`Employee option ${name} is unavailable.`);
    await this.page.getByRole("combobox", { name: "Employee" }).selectOption(value);
    await expect(this.page.getByRole("heading", { name, exact: true })).toBeVisible();
  }

  async updateExistingShift(date: string, reason: string): Promise<void> {
    const shift = this.page.locator(".admin-attendance-shift-card").filter({ hasText: date });
    await expect(shift).toBeVisible();
    await expect(shift.getByRole("button", { name: "Save with audit reason" })).toBeDisabled();
    const clockIn = shift.getByRole("textbox", { name: /Clock In/ });
    const currentClockIn = await clockIn.inputValue();
    const changedClockIn = currentClockIn.endsWith("09:00")
      ? currentClockIn.slice(0, -5) + "09:05"
      : currentClockIn.slice(0, -5) + "09:00";
    await clockIn.fill(changedClockIn);
    await shift.getByRole("textbox", { name: "Reason for Admin change" }).fill(reason);
    await shift.getByRole("button", { name: "Save with audit reason" }).click();
    await expect(this.page.getByRole("status").filter({ hasText: "before/after values" })).toBeVisible();
    await expect(this.page.locator(".admin-audit-card").filter({ hasText: reason })).toBeVisible();
  }
}
