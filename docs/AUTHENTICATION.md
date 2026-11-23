# Authentication and Session Design

## Scope

Authentication, invitations, activation, and profile password management are implemented. Invitation URLs are passed manually by the inviter; the application does not send email. Login is available only for a confirmed Identity account with an active company membership and employee profile.

## Component flow

```mermaid
sequenceDiagram
    actor Employee
    participant React as React client
    participant API as ASP.NET Core API
    participant Auth as EmployeeAuthenticationService
    participant Identity as ASP.NET Core Identity
    participant SQL as SQL Server
    participant Ticket as MemoryCacheTicketStore
    participant Cache as IMemoryCache

    Employee->>React: Enter company name or ID, email, password
    React->>API: GET /api/auth/antiforgery
    API-->>React: Request token + antiforgery cookie
    React->>API: POST /api/auth/login + token header + credentials
    API->>Auth: Authenticate credentials
    Auth->>SQL: Find active company by ID, or exact normalized name
    Auth->>Identity: Find account by normalized email and verify password
    Identity->>SQL: Read account and password hash
    Auth->>SQL: Find active membership, employee profile, and company roles
    Auth-->>API: Authenticated employee context
    API->>Ticket: Store authentication ticket with 12-hour expiry
    Ticket->>Cache: Store ticket under a random session key
    API-->>React: Employee context + protected session cookie
    React->>API: GET /api/auth/me + session cookie
    API->>Ticket: Retrieve ticket by session key
    Ticket->>Cache: Read session ticket
    API-->>React: Current user and selected company membership
    React->>API: POST /api/auth/logout + token header
    API->>Ticket: Remove session ticket
    Ticket->>Cache: Delete session
    API-->>React: Clear cookie; 204 response
```

## Login decisions

- Login accepts the company's five-digit ID or its exact name, plus email and password.
- The server checks the ID first, then an exact case-insensitive company-name match. A duplicated name is ambiguous, so the employee must use the company ID.
- The account must have a confirmed email, an active company, an active membership, and an employee profile.
- Company roles are loaded from that membership. A global user with memberships in multiple companies selects one company by entering its name or ID at login; the session represents only that membership.
- The browser does not submit a user, company, membership, or employee-profile ID as authority. The server derives those values from the verified account and active membership.
- Invalid credentials and membership conditions return the same `401` message to reduce account and company enumeration.
- Login is rate-limited to 15 attempts per minute per source IP as a development safeguard. This is separate from account lockout, which remains deferred. The limit is per API process; deployments behind a proxy must configure trusted forwarded headers before using the client IP for this policy.

## Session storage and cookie

- ASP.NET Core cookie authentication is configured with a custom `ITicketStore` backed by `IMemoryCache`.
- The server stores the claims ticket in memory under a cryptographically random key. The browser receives only the protected session cookie; it does not receive the full claims ticket.
- The absolute session lifetime is 12 hours, configurable through `Authentication:SessionLifetime`. Sliding renewal is disabled. Logout removes the server-side ticket and expires the cookie.
- The production cookie uses the `__Host-` prefix, `Secure`, `HttpOnly`, `SameSite=Lax`, and `Path=/`. Development uses a non-prefixed cookie and matches the request's HTTP/HTTPS setting so the local Vite proxy can be used.
- The antiforgery endpoint returns a request token and stores its paired cookie. Login, logout, password changes, and other state-changing browser requests require the token in the `X-CSRF-TOKEN` header.
- A successful password change removes every active in-memory ticket for that global user, including sessions for their other company memberships, then signs out the current browser. The user must sign in again.
- Authentication tickets are not written to SQL Server. The existing account and membership records remain the source of identity at login.

## Session limits

- Restarting the API process clears all active sessions because the ticket cache is process-local.
- Multiple API instances would need a distributed ticket store shared by all instances, shared ASP.NET Core Data Protection keys, and appropriate load-balancer configuration.
- The login rate limit is also process-local. A multi-instance deployment needs a shared rate-limit strategy and correctly trusted client-IP forwarding.
- Membership changes do not revoke an already-issued ticket immediately. Sessions expire within 12 hours or when the user logs out. Password changes are the exception: they revoke every in-memory ticket for that user immediately.

## Endpoints

| Method | Route | Access | Purpose |
|---|---|---|---|
| `GET` | `/api/auth/antiforgery` | Anonymous | Issue the request token and antiforgery cookie needed for state-changing calls. |
| `POST` | `/api/auth/login` | Anonymous + antiforgery | Verify credentials and establish a session for the selected active membership. |
| `GET` | `/api/auth/me` | Authenticated | Return the current user and session-scoped membership and roles. |
| `POST` | `/api/auth/logout` | Authenticated + antiforgery | Delete the in-memory ticket and clear the session cookie. |
| `GET` | `/api/profile` | Authenticated | Return email, username, and all active company memberships, including company-specific employee and compensation details. |
| `POST` | `/api/profile/password` | Authenticated + antiforgery | Verify the current password, apply the Identity password policy, revoke all of the user's sessions, and sign out the current browser. |
| `GET` | `/api/profile/company-employees` | Admin, HR, or Payroll | List employee compensation settings in the active company. |
| `PUT` | `/api/profile/company-employees/{employeeMembershipId}/compensation` | Admin, HR, or Payroll + antiforgery | Set a company-specific Global or Hourly type and optional numeric hourly rate. |

## Related authentication flows

The invitation API creates, validates, and accepts one-time activation links. New accounts choose a password during activation. An existing confirmed account signs in before accepting an invitation to another company. See [INVITATION_LINKS.md](INVITATION_LINKS.md) for endpoints and security rules. The attendance API uses authenticated employee-profile and company-membership claims without accepting those identifiers from the browser.
