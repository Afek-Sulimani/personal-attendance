import { expect, request, type APIRequestContext } from "@playwright/test";

export const TEST_PASSWORD = process.env.E2E_TEST_PASSWORD ?? "FlowTest!2026";

export type TestActor = {
  email: string;
  displayName: string;
  membershipId: string;
  role: "Admin" | "PayrollRepresentative" | "HR" | "TeamLead" | "Employee";
};

export type SharedWorkspace = {
  companyCode: string;
  companyName: string;
  suffix: string;
  actors: Record<TestActor["role"], TestActor>;
  sessions: Record<TestActor["role"], Awaited<ReturnType<APIRequestContext["storageState"]>>>;
};

type Registration = { companyCode: string; activationUrl: string };
type LoginResult = { membershipId: string; roles: string[] };
type Invitation = { activationUrl: string };

export class TestWorkspace {
  readonly actors = new Map<TestActor["role"], TestActor>();
  readonly companyName: string;
  readonly suffix: string;
  companyCode = "";
  private readonly contexts: APIRequestContext[] = [];
  private readonly sessions = new Map<TestActor["role"], APIRequestContext>();
  private adminContext?: APIRequestContext;

  private constructor(private readonly baseURL: string) {
    this.suffix = `${Date.now()}-${Math.random().toString(36).slice(2, 8)}`;
    this.companyName = `E2E Flow ${this.suffix}`;
  }

  static async create(baseURL: string): Promise<TestWorkspace> {
    if (!["localhost", "127.0.0.1"].includes(new URL(baseURL).hostname)) {
      throw new Error("E2E fixture refuses to create accounts outside localhost.");
    }
    const workspace = new TestWorkspace(baseURL);
    const anonymous = await workspace.context();
    const email = workspace.email("admin");
    const response = await workspace.post<Registration>(anonymous, "/api/companies/registration", {
      companyName: workspace.companyName,
      companyRepresentativeName: "E2E Admin",
      companyRepresentativeEmail: email,
    }, 201);
    workspace.companyCode = response.companyCode;
    await workspace.accept(response.activationUrl);
    workspace.adminContext = await workspace.context();
    const admin = await workspace.login(workspace.adminContext, email);
    workspace.sessions.set("Admin", workspace.adminContext);
    workspace.actors.set("Admin", { email, displayName: "E2E Admin", membershipId: admin.membershipId, role: "Admin" });
    return workspace;
  }

  actor(role: TestActor["role"]): TestActor {
    const actor = this.actors.get(role);
    if (!actor) throw new Error(`${role} was not created for this test.`);
    return actor;
  }

  async invite(role: TestActor["role"], managerRole: TestActor["role"] = "Admin"): Promise<TestActor> {
    if (role === "Admin") throw new Error("This helper creates one Admin per company.");
    const email = this.email(role.toLowerCase());
    const displayName = `E2E ${role === "PayrollRepresentative" ? "Payroll" : role}`;
    const result = await this.post<Invitation>(this.adminContext!, "/api/employee-invitations", {
      email,
      displayName,
      managerMembershipId: this.actor(managerRole).membershipId,
      roles: [role],
    }, 201);
    await this.accept(result.activationUrl);
    const session = await this.context();
    const login = await this.login(session, email);
    this.sessions.set(role, session);
    const actor = { email, displayName, membershipId: login.membershipId, role };
    this.actors.set(role, actor);
    return actor;
  }

  async configureCompany(): Promise<void> {
    await this.put(this.adminContext!, "/api/payroll/company-settings", {
      payrollPeriodStartDay: 16,
      timeZoneId: "Asia/Jerusalem",
      defaultWorkStartTime: "09:00",
      defaultWorkEndTime: "17:00",
      defaultWorkDays: ["Sunday", "Monday", "Tuesday", "Wednesday", "Thursday", "Saturday"],
    });
  }

  async dispose(): Promise<void> {
    await Promise.all(this.contexts.map(context => context.dispose()));
  }

  async snapshot(): Promise<SharedWorkspace> {
    const roles: TestActor["role"][] = ["Admin", "PayrollRepresentative", "HR", "TeamLead", "Employee"];
    const actors = {} as SharedWorkspace["actors"];
    const sessions = {} as SharedWorkspace["sessions"];
    for (const role of roles) {
      actors[role] = this.actor(role);
      sessions[role] = await this.sessions.get(role)!.storageState();
    }
    return { companyCode: this.companyCode, companyName: this.companyName, suffix: this.suffix, actors, sessions };
  }

  private email(label: string): string {
    return `${label}-${this.suffix}@example.test`;
  }

  private async context(): Promise<APIRequestContext> {
    const context = await request.newContext({ baseURL: this.baseURL });
    this.contexts.push(context);
    return context;
  }

  private async accept(activationUrl: string): Promise<void> {
    const context = await this.context();
    const token = new URL(activationUrl).hash.replace(/^#token=/, "");
    await this.post(context, "/api/employee-invitations/accept", { token, password: TEST_PASSWORD }, 200);
  }

  private async login(context: APIRequestContext, email: string): Promise<LoginResult> {
    return this.post<LoginResult>(context, "/api/auth/login", {
      companyIdentifier: this.companyCode,
      email,
      password: TEST_PASSWORD,
    }, 200);
  }

  private async csrf(context: APIRequestContext): Promise<string> {
    const response = await context.get("/api/auth/antiforgery");
    expect(response.ok(), await response.text()).toBeTruthy();
    return (await response.json() as { requestToken: string }).requestToken;
  }

  private async post<T>(context: APIRequestContext, path: string, data: unknown, status: number): Promise<T> {
    const response = await context.post(path, { data, headers: { "X-CSRF-TOKEN": await this.csrf(context) } });
    expect(response.status(), `${path}: ${await response.text()}`).toBe(status);
    return await response.json() as T;
  }

  private async put(context: APIRequestContext, path: string, data: unknown): Promise<void> {
    const response = await context.put(path, { data, headers: { "X-CSRF-TOKEN": await this.csrf(context) } });
    expect(response.status(), `${path}: ${await response.text()}`).toBe(200);
  }
}
