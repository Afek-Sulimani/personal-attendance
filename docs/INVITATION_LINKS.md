# Employee Invitation Links

## Status

The backend invitation API, SQL Server migration, React activation page, and employee-invitation management screen are implemented. Company registration issues the initial Admin invitation.

## Delivery decision

- An Admin, HR, or Payroll Representative creates an employee invitation through the authenticated API.
- The API returns a one-time activation URL. The Admin, HR, or Payroll Representative copies it and delivers it to the employee using a channel they choose.
- The application does not send invitation emails. No SMTP, Gmail, Mailgun, or other email-delivery integration is part of this flow.
- Employee email remains an account identifier. The activation link marks the invited email as confirmed; because delivery is manual, the application itself does not independently prove mailbox ownership. The authorized inviter is responsible for delivering the link to the intended employee.

## Activation flow

1. An Admin, HR, or Payroll Representative submits the employee's email, display name, and direct manager, and selects company roles. The system assigns the employee number. A selected manager must be an active company member with the Admin, HR, or TeamLead role.
2. The API checks that the inviter has an active membership in the selected company. Admin, HR, and Payroll may assign supported roles, but only Admin may assign the Admin role.
3. The API creates an invitation and generates a cryptographically random 256-bit token.
4. SQL Server stores one invitation record and only the token's SHA-256 hash. The raw token is returned once, in the URL fragment, which browsers do not send to the web server automatically.
5. The invitation expires 48 hours after creation. Creating a replacement invitation for the same company and email revokes an earlier unaccepted link.
6. The employee's activation page sends the token to the validation API, then submits it with a password to accept the invitation. Acceptance creates the account if needed, creates the company membership and employee profile, confirms the invited email, and marks the invitation as accepted.
7. An existing confirmed account must sign in as the invited user before accepting an invitation to another company. This preserves one global user account across companies.
8. An invitation can be accepted once. The raw token is not stored or written to application logs.

## API contract

All request and response bodies use JSON. Mutating browser requests require the existing antiforgery cookie and `X-CSRF-TOKEN` header.

For browser clients, first call `GET /api/auth/antiforgery` and send its `requestToken` in `X-CSRF-TOKEN` on create and accept requests. Validation is read-only and does not require antiforgery. The React activation page reads the token from the URL fragment and immediately removes the fragment from browser history after reading it.

| Method and route | Access | Behavior |
|---|---|---|
| `POST /api/employee-invitations` | Authenticated Admin, HR, or Payroll; antiforgery required | Creates a company-scoped invitation and returns its URL once. |
| `POST /api/employee-invitations/validate` | Anonymous | Validates the token and returns invited email, display name, expiration, and company code. |
| `POST /api/employee-invitations/accept` | Anonymous or the invited signed-in user; antiforgery required | Creates or reuses the identity account, creates its company membership/profile, and consumes the invitation. |
| `POST /api/companies/registration` | Anonymous company registration; antiforgery required | Creates a company and issues the first company representative invitation. |

Create request example:

```json
{
  "email": "employee@example.com",
  "displayName": "Example Employee",
  "employeeNumber": "EMP-104",
  "managerMembershipId": null,
  "roles": ["Employee"]
}
```

Create response example:

```json
{
  "invitationId": "00000000-0000-0000-0000-000000000001",
  "activationUrl": "http://localhost:5173/activate#token=<one-time-token>",
  "expiresAtUtc": "2030-02-05T10:00:00+00:00"
}
```

Validation request body:

```json
{ "token": "<one-time-token>" }
```

Acceptance request body for a new account:

```json
{ "token": "<one-time-token>", "password": "<password chosen by employee>" }
```

Acceptance response:

```json
{ "companyCode": "12345" }
```

Invalid, expired, revoked, and already accepted links return the same `404` response. Existing confirmed accounts must sign in before acceptance (`409`); password-policy failures return `400`; authorization failures return `403`; unavailable SQL Server returns `503`.

## Persistence and security

The implementation uses one `ATTENDANCE_EMPLOYEE_INVITATIONS` table. It stores invitation identity, company, optional inviter and manager memberships, invited email, employee details, assigned roles, token hash, timestamps, acceptance user, revocation timestamp, and a SQL Server row version. The inviter membership is null for the initial company representative invitation created during company registration. The row version prevents two concurrent requests from accepting the same invitation.

The token expires exactly 48 hours after creation. Expiration uses the application `TimeProvider` in UTC; it is not an attendance timestamp and does not use the external attendance clock. The token is 256 bits of cryptographic randomness, stored as a SHA-256 hash, and returned only when the invitation is created.

## Configuration and open decisions

- The URL's base address comes from `InvitationLinks:PublicBaseUrl`. The API does not build activation URLs from an untrusted request `Host` header.
- Development uses `http://localhost:5173` from `appsettings.Development.json`. Production must configure `InvitationLinks__PublicBaseUrl` with an HTTPS address; an employee using another computer needs a reachable public URL, such as a temporary tunnel or hosted environment.
- The React `/activate` page and `/admin/people` employee-invitation management form are implemented.
- The activation page does not show the expiration time to the employee. The company registration result displays `The activation link expires 48 hours.` without an exact date or time.
- An existing confirmed user must authenticate as the invited account before the new company membership is added. New users and existing unconfirmed users set or reset their password using the invitation.
- Password checks use ASP.NET Core Identity's configured policy: at least six characters with uppercase and lowercase letters, a digit, and a non-alphanumeric symbol. Activation and password-change screens show the same requirements.

## Scope boundary

This decision concerns employee invitations. Payroll reminders and other possible future notifications remain separate product decisions.

## Original delivery plan

The initial plan was to email the activation link to the employee. Email delivery was explicitly removed from this version; the authorized inviter shares the link manually. The original monthly manager email/approval idea and its replacement are documented in [DECISIONS.md](../DECISIONS.md#original-email-workflow-and-final-scope).
