# Release validation

This personal attendance platform defaults to Israel, supports configurable time zones,
personal and team CSV exports, optional Google authentication and background recovery
of failed time providers. See PERSONAL_EDITION.md for setup and limitations.

Validation completed locally:
- 77 backend tests passed.
- Frontend production build and E2E TypeScript checks passed.
- 27 SQL-backed Playwright user flows passed on a disposable database.
- 3 isolated browser tests passed for CSV export and Google handoff availability.
- Database migrations applied successfully to a new database.

Real Google consent/callback requires OAuth credentials and remains unverified.
LLM assistance is a documented proposal and is not enabled.
