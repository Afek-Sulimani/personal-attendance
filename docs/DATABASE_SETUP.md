# Local SQL Server Setup

This repository supports two local development options:

1. SQL Server 2022 Developer in Docker Compose.
2. An existing SQL Server instance, configured through a local connection string.

The EF Core migrations create the account, company membership, employee profile, role assignment, work-shift, employee invitation, monthly work schedule, monthly workday, and attendance correction tables. They do not add sample data. Apply all migrations before using the attendance, Payroll workday, or correction-review screens.

## Docker Compose

### Platform note for Apple Silicon

The development Mac used for this project has an Apple Silicon M4 processor. The Compose service requests `linux/amd64` so Docker Desktop can attempt x86-64 emulation. Microsoft documents SQL Server Linux container images for Intel/AMD x86-64 and says emulation environments are not tested or supported. This is therefore a best-effort local development setup, not a Microsoft-supported Apple Silicon configuration. A reviewer can use Docker on an x86-64 host or connect the application to an existing SQL Server instance.

See Microsoft's [SQL Server on Linux release information](https://learn.microsoft.com/en-us/sql/linux/sql-server-linux-release-notes?view=sql-server-ver15) and [SQL Server 2022 Docker quickstart](https://learn.microsoft.com/en-us/sql/linux/install-upgrade/quickstart-install-docker).

### Start the container

1. Install Docker Desktop with Docker Compose v2, or install Docker Engine with the Compose plugin.
2. Copy `.env.example` to `.env` and set a unique, strong value for `MSSQL_SA_PASSWORD`. Use a shell-safe alphanumeric value with uppercase, lowercase, and numeric characters (at least eight characters); this satisfies SQL Server's default password policy and allows the `source .env` command below to load it safely. Never commit `.env`.
3. Start the service:

   ```bash
   docker compose up -d
   ```

4. Wait until the `sqlserver` service is `healthy`:

   ```bash
   docker compose ps
   ```

Compose runs SQL Server 2022 Developer, maps it to `127.0.0.1:14333`, and stores database files in a named Docker volume. The official image does not create `PersonalAttendance` or an application login from `MSSQL_DB`, `MSSQL_USER`, or `MSSQL_PASSWORD`; create the database explicitly below. Developer edition is for non-production use. For an isolated evaluator run, use `docs/SYSTEM_TESTER_RUNBOOK.md` instead.

### Configure the API and apply the migration

The API runs on the host and connects to `127.0.0.1,14333`. For a local development database, use the local `sa` account to create the database and apply migrations. Do not use these credentials in a deployed environment.

On macOS or Linux, load the ignored `.env` values into the current shell:

```bash
set -a
source .env
set +a
```

Create the database (the Docker image does not do this automatically):

```bash
SQL_CONTAINER="$(docker compose ps -q sqlserver)"
docker exec "$SQL_CONTAINER" /opt/mssql-tools18/bin/sqlcmd \
  -S localhost -U sa -P "$MSSQL_SA_PASSWORD" -C \
  -Q "IF DB_ID(N'PersonalAttendance') IS NULL CREATE DATABASE [PersonalAttendance];"
```

Store the local connection string in .NET User Secrets:

```bash
dotnet user-secrets --project src/backend/Personal.Attendance.Api/Personal.Attendance.Api.csproj set "ConnectionStrings:Attendance" "Server=127.0.0.1,14333;Database=PersonalAttendance;User Id=sa;Password=${MSSQL_SA_PASSWORD};Encrypt=True;TrustServerCertificate=True;"
```

The design-time EF Core factory reads `ConnectionStrings__Attendance` from the environment, so export that same local connection string before applying the migration:

```bash
export ConnectionStrings__Attendance="Server=127.0.0.1,14333;Database=PersonalAttendance;User Id=sa;Password=${MSSQL_SA_PASSWORD};Encrypt=True;TrustServerCertificate=True;"
dotnet tool restore
dotnet ef database update \
  --project src/backend/Personal.Attendance.Infrastructure/Personal.Attendance.Infrastructure.csproj \
  --startup-project src/backend/Personal.Attendance.Api/Personal.Attendance.Api.csproj
unset ConnectionStrings__Attendance
```

The local `sa` account has the schema privileges needed by EF migrations. Use a separately provisioned, limited login for a deployed environment.

Run the API after the migration:

```bash
dotnet run --project src/backend/Personal.Attendance.Api/Personal.Attendance.Api.csproj
```

Stop the container without deleting its data:

```bash
docker compose down
```

The named volume preserves database files across container stops and recreation. To intentionally delete the local database and start over, run `docker compose down --volumes`; this permanently removes the volume's data.

## Connect Rider to the Docker database

1. Open Rider's **Database** tool window.
2. Select **Add** → **Data Source** → **Microsoft SQL Server**.
3. Enter host `127.0.0.1`, port `14333`, and database `PersonalAttendance`.
4. Use SQL Server authentication with username `sa` and the local `MSSQL_SA_PASSWORD` from `.env`.
5. Download the Microsoft SQL Server JDBC driver if Rider offers that link.
6. In the SSL settings, trust the local development certificate, then select **Test Connection**.

Rider stores the data source in the local IDE configuration. Do not commit Rider project metadata or database credentials. See JetBrains' [Microsoft SQL Server connection guide](https://www.jetbrains.com/help/rider/Microsoft_SQL_Server.html).

## Use an existing SQL Server instance

Docker is optional. If a SQL Server instance is already available, set `ConnectionStrings:Attendance` in .NET User Secrets to its connection string, make the equivalent `ConnectionStrings__Attendance` value available to the migration command, then run the same `dotnet ef database update` command above. The database account must be allowed to create or update the schema for the migration.

## Inspect the migration without connecting

Generate a SQL script:

```bash
dotnet ef migrations script --idempotent \
  --project src/backend/Personal.Attendance.Infrastructure/Personal.Attendance.Infrastructure.csproj \
  --startup-project src/backend/Personal.Attendance.Api/Personal.Attendance.Api.csproj
```

Check for unrepresented model changes:

```bash
dotnet ef migrations has-pending-model-changes \
  --project src/backend/Personal.Attendance.Infrastructure/Personal.Attendance.Infrastructure.csproj \
  --startup-project src/backend/Personal.Attendance.Api/Personal.Attendance.Api.csproj
```

The application does not automatically apply migrations at startup. The local database and Rider connection are developer-specific and are not committed to the repository.

After applying `20261121083759_CompanyAdminSetupAndTimeZones`, sign out and sign in again so existing first representatives receive updated Admin claims. The migration converts only accepted first-representative memberships that still have the former exact HR + Payroll role set. It also creates a trigger that rejects audit-row updates and deletes.
