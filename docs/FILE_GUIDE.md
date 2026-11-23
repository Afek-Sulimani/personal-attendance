# Current File Guide

## Core SQL Server persistence

| File or folder | What it contains |
|---|---|
| `DATABASE_DESIGN.md` | Diagram and explanation of the approved core tables and relationships. |
| `docs/DATABASE_SETUP.md` | Local SQL Server connection, migration, and inspection commands. |
| `docs/API_ENDPOINTS.md` | Complete HTTP endpoint inventory, access scope, and response/action descriptions. |
| `src/backend/Personal.Attendance.Domain/Entities/Company.cs` | Company code, name, and active state. |
| `src/backend/Personal.Attendance.Domain/Entities/CompanyMembership.cs` | A user's membership in a company and direct-manager link. |
| `src/backend/Personal.Attendance.Domain/Entities/MembershipRole.cs` and `Enums/CompanyRole.cs` | Multiple company roles for one membership. |
| `src/backend/Personal.Attendance.Domain/Entities/EmployeeProfile.cs` | Company-specific display name, employee number, compensation type, and optional numeric hourly rate. |
| `src/backend/Personal.Attendance.Domain/Entities/WorkShift.cs` | Clock-in/out instants, each punch's saved time zone and provider provenance, work date, UTC validation, and minute truncation. |
| `src/backend/Personal.Attendance.Infrastructure/Persistence/PersonalAttendanceDbContext.cs` | Identity-aware EF Core context, DbSets, and `ATTENDANCE_*` Identity table names. |
| `src/backend/Personal.Attendance.Infrastructure/Persistence/Configurations/` | Explicit SQL Server column, key, index, constraint, and relationship mappings. |
| `src/backend/Personal.Attendance.Infrastructure/Persistence/Migrations/` | SQL Server migrations and model snapshot generated from the current context, including Admin settings and attendance audit. |
| `src/backend/Personal.Attendance.Infrastructure/Persistence/PersonalAttendanceDbContextFactory.cs` | Design-time SQL Server context for EF tooling; reads its connection from an environment variable. |
| `src/backend/Personal.Attendance.Infrastructure/Persistence/PersonalUser.cs` and `PersonalRole.cs` | GUID-based ASP.NET Core Identity account and global-role types. |
| `dotnet-tools.json` | Pins the Entity Framework command-line tool used for migrations. |

## Verification

| File or folder | What it verifies |
|---|---|
| `src/backend/Personal.Attendance.Tests/Domain/` | Company normalization and work-shift event rules using fixed timestamps. |
| `src/backend/Personal.Attendance.Tests/Persistence/` | SQL Server EF metadata: table names, tenant-safe indexes, open-shift uniqueness, and concurrency token. |
| `.claude/skills/dotnet-unit-tests/SKILL.md` | Deterministic .NET unit-test conventions and the test command. |
| `.claude/skills/code-review/SKILL.md` | Review-only security, company-isolation, time-integrity, and maintainability checks. |

## Authentication and sessions

| File or folder | What it contains |
|---|---|
| `docs/AUTHENTICATION.md` | Login sequence, API routes, CSRF behavior, cookie/session rules, and known limits. |
| `src/backend/Personal.Attendance.Application/Authentication/` | Login credentials, authenticated employee result, and the application service contract. |
| `src/backend/Personal.Attendance.Infrastructure/Authentication/EmployeeAuthenticationService.cs` | Company code, email confirmation, password, active membership, profile, and role lookup. |
| `src/backend/Personal.Attendance.Api/Controllers/AuthenticationController.cs` | Antiforgery, login, current-session, and logout HTTP endpoints. |
| `src/backend/Personal.Attendance.Api/Authentication/MemoryCacheTicketStore.cs` | Server-side authentication tickets in process memory, removed on logout, expiration, or password change for every session belonging to the user. |
| `src/backend/Personal.Attendance.Api/Controllers/EmployeeProfileController.cs` | Self-profile, password-change, company employee list, and compensation endpoints. |
| `src/backend/Personal.Attendance.Infrastructure/Profiles/EmployeeProfileService.cs` | Reads active multi-company profile details, verifies change-password and manager access, and saves company-scoped compensation. |
| `src/backend/Personal.Attendance.Application/Profiles/` | Profile response contracts, actor scope, service interface, and expected errors. |
| `src/backend/Personal.Attendance.Tests/Profiles/EmployeeProfileServiceTests.cs` | Deterministic tests for company profile scope, compensation permissions, and password validation. |
| `src/frontend/personal-web/src/pages/EmployeeProfilePage.tsx` | Employee's account details, active company memberships, compensation display, and password change. |
| `src/frontend/personal-web/src/pages/EmployeeCompensationPage.tsx` | Admin/HR/Payroll screen to configure per-employee Global or Hourly pay type. |
| `src/frontend/personal-web/src/features/profile/profileApi.ts` | Typed React client calls for profile and compensation endpoints. |
| `src/backend/Personal.Attendance.Api/Authentication/AuthenticationServiceCollectionExtensions.cs` | Cookie, antiforgery, ticket-store, and login rate-limit registration. |
| `src/backend/Personal.Attendance.Api/Authentication/AttendanceAuthenticationDefaults.cs` | Authentication scheme and membership-scoped claim names. |
| `src/backend/Personal.Attendance.Api/Authentication/UnavailableEmployeeAuthenticationService.cs` | Returns a controlled unavailable response when SQL Server is not configured. |
| `src/backend/Personal.Attendance.Tests/Authentication/` | Identity lookup and in-memory ticket-store tests. |

## Monthly attendance views and browser flows

| File or folder | What it contains |
|---|---|
| `src/backend/Personal.Attendance.Infrastructure/Attendance/MonthlyAttendanceReportService.cs` | Read-only workday rows for a configured payroll period, approved shift durations, pending correction hours, and team access checks. |
| `src/backend/Personal.Attendance.Infrastructure/Attendance/CorrectionAttentionPolicy.cs` | Cutoff rule for marking pending corrections after their payroll period ends. |
| `src/frontend/personal-web/src/features/reports/MonthlyReportDaysTable.tsx` | Compact employee and manager table with one row per scheduled workday or pending correction. |
| `src/frontend/personal-web/src/features/reports/monthlyAttendanceReportApi.ts` | Typed client for employee and team payroll-period views. |
| `src/frontend/personal-web/src/pages/MonthlyAttendanceReportPage.tsx` and `TeamMonthlyAttendancePage.tsx` | Employee and manager views, payroll-period navigation, total hours, and separate awaiting-approval hours. |
| `src/frontend/personal-web/e2e/scenarios/` | One Playwright spec per user flow, including payroll-period boundaries, correction visibility, approval, rejection, role access, mobile navigation, and onboarding. |
| `src/frontend/personal-web/e2e/pages/BasePage.ts` and `ApplicationPage.ts` | Shared page-object base classes for route opening, headings, role navigation, sign out, and screenshots. |
| `src/frontend/personal-web/e2e/support/TestWorkspace.ts` | Local-only test company and role-account setup with one shared test password. |
| `docs/user-flows/SCENARIOS.md` | Full user-flow catalog and mapping from roles to expected behavior. |

## Employee invitations

| File | What it contains |
|---|---|
| `docs/INVITATION_LINKS.md` | Invitation API contract, 48-hour expiry, manual link delivery, and token security. |
| `docs/COMPANY_REGISTRATION.md` | Company registration flow and the first representative invitation. |
| `src/backend/Personal.Attendance.Domain/Entities/EmployeeInvitation.cs` | Invitation details, assigned roles, token hash, expiry, and single-use state transitions. |
| `src/backend/Personal.Attendance.Infrastructure/Invitations/EmployeeInvitationService.cs` | Authorization, secure token creation, invitation persistence, account activation, and company membership creation. |
| `src/backend/Personal.Attendance.Api/Controllers/EmployeeInvitationsController.cs` | Create, validate, and accept invitation HTTP endpoints. |
| `src/backend/Personal.Attendance.Api/Controllers/CompanyRegistrationController.cs` | Company registration HTTP endpoint and request/response validation. |
| `src/backend/Personal.Attendance.Infrastructure/Companies/CompanyRegistrationService.cs` | Five-digit company code generation and atomic company plus first-invitation creation. |
| `src/frontend/personal-web/src/pages/ActivationPage.tsx` | Invitation validation, password setup, and account activation. |
| `src/frontend/personal-web/src/pages/CompanyRegistrationPage.tsx` | Company registration form and generated company ID/activation-link result. |
| `src/frontend/personal-web/src/pages/LoginPage.tsx` | Company-scoped employee sign-in and session state. |
| `src/backend/Personal.Attendance.Api/appsettings.Development.json` | Local React base URL used to build activation links; production must set an HTTPS `InvitationLinks__PublicBaseUrl`. |
| `src/backend/Personal.Attendance.Infrastructure/Persistence/Configurations/EmployeeInvitationConfiguration.cs` | SQL Server keys, relationships, constraints, and token-hash index. |
| `src/backend/Personal.Attendance.Infrastructure/Persistence/Migrations/*EmployeeInvitations*` | SQL Server migration for the invitation table. |
| `src/backend/Personal.Attendance.Tests/Invitations/EmployeeInvitationServiceTests.cs` | Deterministic tests for link expiry, one-time acceptance, role permissions, and multi-company accounts. |

## Attendance API

| File or folder | What it contains |
|---|---|
| `docs/ATTENDANCE_PUNCH_DESIGN.md` | Attendance workspace routes, time-source rules, correction approval, and calendar behavior. |
| `src/backend/Personal.Attendance.Application/Attendance/` | Actor scope, calendar and correction contracts, service interfaces, results, and expected errors. |
| `src/backend/Personal.Attendance.Infrastructure/Attendance/AttendanceService.cs` | Live punch validation, monthly calendar projection, schedule enforcement, and shift persistence. |
| `src/backend/Personal.Attendance.Infrastructure/Attendance/AttendanceCorrectionService.cs` | Historical request validation, assigned-manager authorization, company fallback, approval transaction, and rejection audit. |
| `src/backend/Personal.Attendance.Infrastructure/Payroll/PayrollWorkScheduleService.cs` | Company-scoped employee selection and per-month workday schedule persistence. |
| `src/backend/Personal.Attendance.Api/Controllers/AttendanceController.cs` | Current-shift, calendar, and live punch HTTP endpoints. |
| `src/backend/Personal.Attendance.Api/Controllers/AttendanceCorrectionsController.cs` | Correction submission, pending review, approval, and rejection endpoints. |
| `src/backend/Personal.Attendance.Api/Controllers/PayrollEmployeesController.cs` | Payroll employee list and monthly workday endpoints. |
| `src/backend/Personal.Attendance.Infrastructure/Persistence/Migrations/*AttendanceWorkspace*` | SQL Server changes for work schedules, correction requests, and time-source provenance. |
| `src/frontend/personal-web/src/pages/AttendancePage.tsx` | Employee punch controls, calendar, selected-day details, and correction request form. |
| `src/frontend/personal-web/src/pages/PayrollWorkDaysPage.tsx` | Payroll employee selection and monthly workday configuration. |
| `src/frontend/personal-web/src/pages/AttendanceCorrectionReviewPage.tsx` | Manager approval/rejection queue with required rejection reason. |
| `src/frontend/personal-web/src/components/ApplicationLayout.tsx` | Responsive sidebar and navigation filtered by company roles. |

## Company Admin setup and attendance audit

| File or folder | What it contains |
|---|---|
| `src/backend/Personal.Attendance.Domain/Entities/CompanyPayrollSettings.cs` | One company’s payroll cycle start day, time zone, standard hours, and settings audit fields. |
| `src/backend/Personal.Attendance.Domain/Entities/CompanyDefaultWorkDay.cs` | One normalized row for each company default weekday, keyed by company and weekday. |
| `src/backend/Personal.Attendance.Infrastructure/Payroll/CompanyPayrollSettingsService.cs` | Company-scoped authorization and persistence for company settings and default weekdays. |
| `src/backend/Personal.Attendance.Api/Controllers/CompanyPayrollSettingsController.cs` | Read and update endpoints for Admin or Payroll company settings. |
| `src/backend/Personal.Attendance.Api/Time/CompanyTimeZoneRegistry.cs` | Validates and registers configured system time-zone IDs for the synchronized clock. |
| `src/backend/Personal.Attendance.Domain/Entities/AdminAttendanceChangeLog.cs` | Before/after punch values, dates, source provenance, actor, reason, and external audit-time provenance. |
| `src/backend/Personal.Attendance.Infrastructure/Attendance/AdminAttendanceService.cs` | Admin authorization, existing-shift-only edits, time validation, and transactional audit persistence. |
| `src/backend/Personal.Attendance.Api/Controllers/AdminAttendanceController.cs` | Read monthly shifts/audit records and update one existing shift. |
| `src/frontend/personal-web/src/pages/CompanyPayrollSettingsPage.tsx` | Company payroll, time-zone, standard-hour, and default-weekday form. |
| `src/frontend/personal-web/src/pages/EmployeeInvitationsPage.tsx` | Invite employees, assign allowed roles, and choose their direct manager. |
| `src/frontend/personal-web/src/pages/AdminAttendancePage.tsx` | Select a company employee/month, edit existing shifts, and review the before/after audit. |
| `src/backend/Personal.Attendance.Infrastructure/Persistence/Migrations/20261121083759_CompanyAdminSetupAndTimeZones.cs` | Adds company settings, per-punch time zones/work dates, and Admin attendance audit storage; backfills existing punch zones as Israel. |

The first company representative receives `Admin`. Admin may configure the company, invite all supported roles, and correct existing company shifts, including their own; each correction requires a reason and an audit record. Admin editing does not create a missing shift or change an open shift into a closed shift (or vice versa).

## Boundaries

The application includes monthly attendance reports and company-specific compensation settings, but it does not calculate wages, taxes, deductions, or leave balances. Invitation links are passed manually; the application does not send email.
