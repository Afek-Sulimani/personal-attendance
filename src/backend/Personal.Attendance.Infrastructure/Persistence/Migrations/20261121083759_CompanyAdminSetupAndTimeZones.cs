using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Personal.Attendance.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class CompanyAdminSetupAndTimeZones : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_ATTENDANCE_WORK_SHIFTS_CLOCK_IN_SOURCE",
                table: "ATTENDANCE_WORK_SHIFTS");

            migrationBuilder.DropCheckConstraint(
                name: "CK_ATTENDANCE_WORK_SHIFTS_CLOCK_OUT_SOURCE",
                table: "ATTENDANCE_WORK_SHIFTS");

            migrationBuilder.AddColumn<string>(
                name: "ClockInTimeZoneId",
                table: "ATTENDANCE_WORK_SHIFTS",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ClockOutTimeZoneId",
                table: "ATTENDANCE_WORK_SHIFTS",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "WorkDate",
                table: "ATTENDANCE_WORK_SHIFTS",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TimeZoneId",
                table: "ATTENDANCE_ATTENDANCE_CORRECTION_REQUESTS",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            // Existing attendance records were captured in the product's original Asia/Jerusalem time zone.
            // SQL Server uses the Windows identifier for the same zone when backfilling the local work date.
            migrationBuilder.Sql(
                "UPDATE [ATTENDANCE_WORK_SHIFTS] SET " +
                "[ClockInTimeZoneId] = N'Asia/Jerusalem', " +
                "[WorkDate] = CONVERT(date, [ClockInAtUtc] AT TIME ZONE 'W. Europe Standard Time'), " +
                "[ClockOutTimeZoneId] = CASE WHEN [ClockOutAtUtc] IS NULL THEN NULL ELSE N'Asia/Jerusalem' END;");
            migrationBuilder.Sql(
                "UPDATE [ATTENDANCE_ATTENDANCE_CORRECTION_REQUESTS] " +
                "SET [TimeZoneId] = N'Asia/Jerusalem';");

            // Upgrade only accepted first-representative invitations with the exact former HR + Payroll role set.
            migrationBuilder.Sql("""
                DECLARE @LegacyFirstRepresentatives TABLE (MembershipId uniqueidentifier PRIMARY KEY);
                INSERT INTO @LegacyFirstRepresentatives (MembershipId)
                SELECT membership.MembershipId
                FROM ATTENDANCE_EMPLOYEE_INVITATIONS AS invitation
                JOIN ATTENDANCE_COMPANY_MEMBERSHIPS AS membership
                  ON membership.CompanyId = invitation.CompanyId
                 AND membership.UserId = invitation.AcceptedUserId
                WHERE invitation.CreatedByMembershipId IS NULL
                  AND invitation.ManagerMembershipId IS NULL
                  AND invitation.AssignedRoles = N'Hr,PayrollRepresentative'
                  AND invitation.AcceptedUserId IS NOT NULL
                  AND (SELECT COUNT(*) FROM ATTENDANCE_MEMBERSHIP_ROLES AS role
                       WHERE role.MembershipId = membership.MembershipId) = 2
                  AND EXISTS (SELECT 1 FROM ATTENDANCE_MEMBERSHIP_ROLES AS role
                              WHERE role.MembershipId = membership.MembershipId AND role.Role = N'HR')
                  AND EXISTS (SELECT 1 FROM ATTENDANCE_MEMBERSHIP_ROLES AS role
                              WHERE role.MembershipId = membership.MembershipId AND role.Role = N'PayrollRepresentative');

                INSERT INTO ATTENDANCE_MEMBERSHIP_ROLES (MembershipId, Role)
                SELECT MembershipId, N'Admin' FROM @LegacyFirstRepresentatives;

                DELETE role FROM ATTENDANCE_MEMBERSHIP_ROLES AS role
                JOIN @LegacyFirstRepresentatives AS legacy ON legacy.MembershipId = role.MembershipId
                WHERE role.Role IN (N'HR', N'PayrollRepresentative');
                """);

            migrationBuilder.AlterColumn<string>(
                name: "ClockInTimeZoneId",
                table: "ATTENDANCE_WORK_SHIFTS",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(100)",
                oldMaxLength: 100,
                oldNullable: true);

            migrationBuilder.AlterColumn<DateOnly>(
                name: "WorkDate",
                table: "ATTENDANCE_WORK_SHIFTS",
                type: "date",
                nullable: false,
                oldClrType: typeof(DateOnly),
                oldType: "date",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "TimeZoneId",
                table: "ATTENDANCE_ATTENDANCE_CORRECTION_REQUESTS",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(100)",
                oldMaxLength: 100,
                oldNullable: true);

            migrationBuilder.CreateTable(
                name: "ATTENDANCE_ADMIN_ATTENDANCE_CHANGE_LOGS",
                columns: table => new
                {
                    AuditId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EmployeeProfileId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    WorkShiftId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ChangedByMembershipId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PreviousWorkDate = table.Column<DateOnly>(type: "date", nullable: false),
                    WorkDate = table.Column<DateOnly>(type: "date", nullable: false),
                    PreviousClockInAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset(7)", nullable: false),
                    PreviousClockInTimeZoneId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    PreviousClockInSource = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    PreviousClockInProvider = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    PreviousClockInProviderSampleUtc = table.Column<DateTimeOffset>(type: "datetimeoffset(7)", nullable: true),
                    ClockInAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset(7)", nullable: false),
                    ClockInTimeZoneId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ClockInSource = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    ClockInProvider = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ClockInProviderSampleUtc = table.Column<DateTimeOffset>(type: "datetimeoffset(7)", nullable: true),
                    PreviousClockOutAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset(7)", nullable: true),
                    PreviousClockOutTimeZoneId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    PreviousClockOutSource = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: true),
                    PreviousClockOutProvider = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    PreviousClockOutProviderSampleUtc = table.Column<DateTimeOffset>(type: "datetimeoffset(7)", nullable: true),
                    ClockOutAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset(7)", nullable: true),
                    ClockOutTimeZoneId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ClockOutSource = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: true),
                    ClockOutProvider = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ClockOutProviderSampleUtc = table.Column<DateTimeOffset>(type: "datetimeoffset(7)", nullable: true),
                    Reason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    ChangedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset(7)", nullable: false),
                    ChangeTimeZoneId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ChangeTimeProvider = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ChangeProviderSampleUtc = table.Column<DateTimeOffset>(type: "datetimeoffset(7)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ATTENDANCE_ADMIN_ATTENDANCE_CHANGE_LOGS", x => x.AuditId);
                    table.CheckConstraint("CK_ATTENDANCE_ADMIN_ATTENDANCE_CHANGE_LOGS_CLOCK_IN_SOURCE", "([ClockInSource] = 'ExternalApi' AND [ClockInProvider] IS NOT NULL AND [ClockInProviderSampleUtc] IS NOT NULL) OR ([ClockInSource] IN ('ManagerApprovedCorrection', 'AdminDirectCorrection') AND [ClockInProvider] IS NULL AND [ClockInProviderSampleUtc] IS NULL)");
                    table.CheckConstraint("CK_ATTENDANCE_ADMIN_ATTENDANCE_CHANGE_LOGS_CLOCK_OUT_SOURCE", "([ClockOutAtUtc] IS NULL AND [ClockOutTimeZoneId] IS NULL AND [ClockOutSource] IS NULL AND [ClockOutProvider] IS NULL AND [ClockOutProviderSampleUtc] IS NULL) OR ([ClockOutAtUtc] IS NOT NULL AND [ClockOutTimeZoneId] IS NOT NULL AND [ClockOutSource] = 'ExternalApi' AND [ClockOutProvider] IS NOT NULL AND [ClockOutProviderSampleUtc] IS NOT NULL) OR ([ClockOutAtUtc] IS NOT NULL AND [ClockOutTimeZoneId] IS NOT NULL AND [ClockOutSource] IN ('ManagerApprovedCorrection', 'AdminDirectCorrection') AND [ClockOutProvider] IS NULL AND [ClockOutProviderSampleUtc] IS NULL)");
                    table.CheckConstraint("CK_ATTENDANCE_ADMIN_ATTENDANCE_CHANGE_LOGS_PREVIOUS_CLOCK_OUT_SOURCE", "([PreviousClockOutAtUtc] IS NULL AND [PreviousClockOutTimeZoneId] IS NULL AND [PreviousClockOutSource] IS NULL AND [PreviousClockOutProvider] IS NULL AND [PreviousClockOutProviderSampleUtc] IS NULL) OR ([PreviousClockOutAtUtc] IS NOT NULL AND [PreviousClockOutTimeZoneId] IS NOT NULL AND [PreviousClockOutSource] = 'ExternalApi' AND [PreviousClockOutProvider] IS NOT NULL AND [PreviousClockOutProviderSampleUtc] IS NOT NULL) OR ([PreviousClockOutAtUtc] IS NOT NULL AND [PreviousClockOutTimeZoneId] IS NOT NULL AND [PreviousClockOutSource] IN ('ManagerApprovedCorrection', 'AdminDirectCorrection') AND [PreviousClockOutProvider] IS NULL AND [PreviousClockOutProviderSampleUtc] IS NULL)");
                    table.ForeignKey(
                        name: "FK_ATTENDANCE_ADMIN_ATTENDANCE_CHANGE_LOGS_ATTENDANCE_COMPANY_MEMBERSHIPS_CompanyId_ChangedByMembershipId",
                        columns: x => new { x.CompanyId, x.ChangedByMembershipId },
                        principalTable: "ATTENDANCE_COMPANY_MEMBERSHIPS",
                        principalColumns: new[] { "CompanyId", "MembershipId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ATTENDANCE_ADMIN_ATTENDANCE_CHANGE_LOGS_ATTENDANCE_WORK_SHIFTS_CompanyId_WorkShiftId_EmployeeProfileId",
                        columns: x => new { x.CompanyId, x.WorkShiftId, x.EmployeeProfileId },
                        principalTable: "ATTENDANCE_WORK_SHIFTS",
                        principalColumns: new[] { "CompanyId", "WorkShiftId", "EmployeeProfileId" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ATTENDANCE_COMPANY_PAYROLL_SETTINGS",
                columns: table => new
                {
                    CompanyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PayrollPeriodStartDay = table.Column<int>(type: "int", nullable: false),
                    TimeZoneId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    DefaultWorkStartTime = table.Column<TimeOnly>(type: "time(0)", nullable: false),
                    DefaultWorkEndTime = table.Column<TimeOnly>(type: "time(0)", nullable: false),
                    ConfiguredByMembershipId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset(7)", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset(7)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ATTENDANCE_COMPANY_PAYROLL_SETTINGS", x => x.CompanyId);
                    table.CheckConstraint("CK_ATTENDANCE_COMPANY_PAYROLL_SETTINGS_PERIOD_START_DAY", "[PayrollPeriodStartDay] >= 1 AND [PayrollPeriodStartDay] <= 31");
                    table.ForeignKey(
                        name: "FK_ATTENDANCE_COMPANY_PAYROLL_SETTINGS_ATTENDANCE_COMPANIES_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "ATTENDANCE_COMPANIES",
                        principalColumn: "CompanyId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ATTENDANCE_COMPANY_PAYROLL_SETTINGS_ATTENDANCE_COMPANY_MEMBERSHIPS_CompanyId_ConfiguredByMembershipId",
                        columns: x => new { x.CompanyId, x.ConfiguredByMembershipId },
                        principalTable: "ATTENDANCE_COMPANY_MEMBERSHIPS",
                        principalColumns: new[] { "CompanyId", "MembershipId" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ATTENDANCE_COMPANY_DEFAULT_WORK_DAYS",
                columns: table => new
                {
                    CompanyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DayOfWeek = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ATTENDANCE_COMPANY_DEFAULT_WORK_DAYS", x => new { x.CompanyId, x.DayOfWeek });
                    table.ForeignKey(
                        name: "FK_ATTENDANCE_COMPANY_DEFAULT_WORK_DAYS_ATTENDANCE_COMPANY_PAYROLL_SETTINGS_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "ATTENDANCE_COMPANY_PAYROLL_SETTINGS",
                        principalColumn: "CompanyId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.Sql("""
                CREATE TRIGGER [TR_ATTENDANCE_ADMIN_ATTENDANCE_CHANGE_LOGS_PROTECT]
                ON [ATTENDANCE_ADMIN_ATTENDANCE_CHANGE_LOGS]
                AFTER UPDATE, DELETE
                AS
                BEGIN
                    SET NOCOUNT ON;
                    THROW 51001, 'Admin attendance audit records cannot be updated or deleted.', 1;
                END;
                """);

            migrationBuilder.CreateIndex(
                name: "IX_ATTENDANCE_WORK_SHIFTS_CompanyId_EmployeeProfileId_WorkDate",
                table: "ATTENDANCE_WORK_SHIFTS",
                columns: new[] { "CompanyId", "EmployeeProfileId", "WorkDate" });

            migrationBuilder.AddCheckConstraint(
                name: "CK_ATTENDANCE_WORK_SHIFTS_CLOCK_IN_SOURCE",
                table: "ATTENDANCE_WORK_SHIFTS",
                sql: "([ClockInSource] = 'ExternalApi' AND [ClockInProvider] IS NOT NULL AND [ClockInProviderSampleUtc] IS NOT NULL) OR ([ClockInSource] IN ('ManagerApprovedCorrection', 'AdminDirectCorrection') AND [ClockInProvider] IS NULL AND [ClockInProviderSampleUtc] IS NULL)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_ATTENDANCE_WORK_SHIFTS_CLOCK_OUT_SOURCE",
                table: "ATTENDANCE_WORK_SHIFTS",
                sql: "([ClockOutAtUtc] IS NULL AND [ClockOutTimeZoneId] IS NULL AND [ClockOutSource] IS NULL AND [ClockOutProvider] IS NULL AND [ClockOutProviderSampleUtc] IS NULL) OR ([ClockOutAtUtc] IS NOT NULL AND [ClockOutTimeZoneId] IS NOT NULL AND [ClockOutSource] = 'ExternalApi' AND [ClockOutProvider] IS NOT NULL AND [ClockOutProviderSampleUtc] IS NOT NULL) OR ([ClockOutAtUtc] IS NOT NULL AND [ClockOutTimeZoneId] IS NOT NULL AND [ClockOutSource] IN ('ManagerApprovedCorrection', 'AdminDirectCorrection') AND [ClockOutProvider] IS NULL AND [ClockOutProviderSampleUtc] IS NULL)");

            migrationBuilder.CreateIndex(
                name: "IX_ATTENDANCE_ADMIN_ATTENDANCE_CHANGE_LOGS_CompanyId_ChangedByMembershipId_ChangedAtUtc",
                table: "ATTENDANCE_ADMIN_ATTENDANCE_CHANGE_LOGS",
                columns: new[] { "CompanyId", "ChangedByMembershipId", "ChangedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_ATTENDANCE_ADMIN_ATTENDANCE_CHANGE_LOGS_CompanyId_EmployeeProfileId_WorkDate_ChangedAtUtc",
                table: "ATTENDANCE_ADMIN_ATTENDANCE_CHANGE_LOGS",
                columns: new[] { "CompanyId", "EmployeeProfileId", "WorkDate", "ChangedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_ATTENDANCE_ADMIN_ATTENDANCE_CHANGE_LOGS_CompanyId_WorkShiftId_ChangedAtUtc",
                table: "ATTENDANCE_ADMIN_ATTENDANCE_CHANGE_LOGS",
                columns: new[] { "CompanyId", "WorkShiftId", "ChangedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_ATTENDANCE_ADMIN_ATTENDANCE_CHANGE_LOGS_CompanyId_WorkShiftId_EmployeeProfileId",
                table: "ATTENDANCE_ADMIN_ATTENDANCE_CHANGE_LOGS",
                columns: new[] { "CompanyId", "WorkShiftId", "EmployeeProfileId" });

            migrationBuilder.CreateIndex(
                name: "IX_ATTENDANCE_COMPANY_PAYROLL_SETTINGS_CompanyId_ConfiguredByMembershipId",
                table: "ATTENDANCE_COMPANY_PAYROLL_SETTINGS",
                columns: new[] { "CompanyId", "ConfiguredByMembershipId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Keep the migration from dropping Admin correction provenance that the previous app version cannot represent.
            migrationBuilder.Sql(
                "IF EXISTS (SELECT 1 FROM [ATTENDANCE_WORK_SHIFTS] " +
                "WHERE [ClockInSource] = N'AdminDirectCorrection' OR [ClockOutSource] = N'AdminDirectCorrection') " +
                "THROW 51000, 'Cannot revert company Admin attendance changes while Admin-corrected punches exist.', 1;");

            migrationBuilder.DropTable(
                name: "ATTENDANCE_ADMIN_ATTENDANCE_CHANGE_LOGS");

            migrationBuilder.DropTable(
                name: "ATTENDANCE_COMPANY_DEFAULT_WORK_DAYS");

            migrationBuilder.DropTable(
                name: "ATTENDANCE_COMPANY_PAYROLL_SETTINGS");

            migrationBuilder.DropIndex(
                name: "IX_ATTENDANCE_WORK_SHIFTS_CompanyId_EmployeeProfileId_WorkDate",
                table: "ATTENDANCE_WORK_SHIFTS");

            migrationBuilder.DropCheckConstraint(
                name: "CK_ATTENDANCE_WORK_SHIFTS_CLOCK_IN_SOURCE",
                table: "ATTENDANCE_WORK_SHIFTS");

            migrationBuilder.DropCheckConstraint(
                name: "CK_ATTENDANCE_WORK_SHIFTS_CLOCK_OUT_SOURCE",
                table: "ATTENDANCE_WORK_SHIFTS");

            migrationBuilder.DropColumn(
                name: "ClockInTimeZoneId",
                table: "ATTENDANCE_WORK_SHIFTS");

            migrationBuilder.DropColumn(
                name: "ClockOutTimeZoneId",
                table: "ATTENDANCE_WORK_SHIFTS");

            migrationBuilder.DropColumn(
                name: "WorkDate",
                table: "ATTENDANCE_WORK_SHIFTS");

            migrationBuilder.DropColumn(
                name: "TimeZoneId",
                table: "ATTENDANCE_ATTENDANCE_CORRECTION_REQUESTS");

            migrationBuilder.AddCheckConstraint(
                name: "CK_ATTENDANCE_WORK_SHIFTS_CLOCK_IN_SOURCE",
                table: "ATTENDANCE_WORK_SHIFTS",
                sql: "([ClockInSource] = 'ExternalApi' AND [ClockInProvider] IS NOT NULL AND [ClockInProviderSampleUtc] IS NOT NULL) OR ([ClockInSource] = 'ManagerApprovedCorrection' AND [ClockInProvider] IS NULL AND [ClockInProviderSampleUtc] IS NULL)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_ATTENDANCE_WORK_SHIFTS_CLOCK_OUT_SOURCE",
                table: "ATTENDANCE_WORK_SHIFTS",
                sql: "([ClockOutAtUtc] IS NULL AND [ClockOutSource] IS NULL AND [ClockOutProvider] IS NULL AND [ClockOutProviderSampleUtc] IS NULL) OR ([ClockOutAtUtc] IS NOT NULL AND [ClockOutSource] = 'ExternalApi' AND [ClockOutProvider] IS NOT NULL AND [ClockOutProviderSampleUtc] IS NOT NULL) OR ([ClockOutAtUtc] IS NOT NULL AND [ClockOutSource] = 'ManagerApprovedCorrection' AND [ClockOutProvider] IS NULL AND [ClockOutProviderSampleUtc] IS NULL)");
        }
    }
}
