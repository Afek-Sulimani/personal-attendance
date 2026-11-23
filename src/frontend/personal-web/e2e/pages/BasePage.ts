import { expect, type Page, type TestInfo } from "@playwright/test";

export abstract class BasePage {
  protected constructor(
    protected readonly page: Page,
    private readonly path: string,
    private readonly heading: string,
  ) {}

  async open(): Promise<void> {
    await this.page.goto(this.path);
    await this.ready();
  }

  async ready(): Promise<void> {
    await expect(this.page.getByRole("heading", { name: this.heading, exact: true })).toBeVisible();
  }

  async capture(testInfo: TestInfo, name: string): Promise<void> {
    await testInfo.attach(name, {
      body: await this.page.screenshot({ fullPage: true }),
      contentType: "image/png",
    });
  }
}
