using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Personal.Attendance.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AttendanceWorkspace : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "ClockInProviderSampleUtc",
                table: "ATTENDANCE_WORK_SHIFTS",
                type: "datetimeoffset(7)",
                nullable: true,
                oldClrType: typeof(DateTimeOffset),
                oldType: "datetimeoffset(7)");

            migrationBuilder.AlterColumn<string>(
                name: "ClockInProvider",
                table: "ATTENDANCE_WORK_SHIFTS",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(100)",
                oldMaxLength: 100);

            migrationBuilder.AddColumn<string>(
                name: "ClockInSource",
                table: "ATTENDANCE_WORK_SHIFTS",
                type: "nvarchar(40)",
                maxLength: 40,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ClockOutSource",
                table: "ATTENDANCE_WORK_SHIFTS",
                type: "nvarchar(40)",
                maxLength: 40,
                nullable: true);

            migrationBuilder.Sql(
                "UPDATE [ATTENDANCE_WORK_SHIFTS] " +
                "SET [ClockInSource] = 'ExternalApi', " +
                "[ClockOutSource] = CASE WHEN [ClockOutAtUtc] IS NULL THEN NULL ELSE 'ExternalApi' END;");

            migrationBuilder.AlterColumn<string>(
                name: "ClockInSource",
                table: "ATTENDANCE_WORK_SHIFTS",
                type: "nvarchar(40)",
                maxLength: 40,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(40)",
                oldMaxLength: 40,
                oldNullable: true);

            migrationBuilder.CreateTable(
                name: "ATTENDANCE_ATTENDANCE_CORRECTION_REQUESTS",
                columns: table => new
                {
                    RequestId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EmployeeProfileId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RequestedByMembershipId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    WorkShiftId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DirectApproverMembershipId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    WorkDate = table.Column<DateOnly>(type: "date", nullable: false),
                    RequestedClockInAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset(7)", nullable: true),
                    RequestedClockOutAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset(7)", nullable: true),
                    SubmittedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset(7)", nullable: false),
                    SubmissionTimeProvider = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    SubmissionProviderSampleUtc = table.Column<DateTimeOffset>(type: "datetimeoffset(7)", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    ReviewedByMembershipId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ReviewedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset(7)", nullable: true),
                    RejectionReason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ATTENDANCE_ATTENDANCE_CORRECTION_REQUESTS", x => x.RequestId);
                    table.UniqueConstraint("AK_ATTENDANCE_ATTENDANCE_CORRECTION_REQUESTS_CompanyId_RequestId", x => new { x.CompanyId, x.RequestId });
                    table.CheckConstraint("CK_ATTENDANCE_ATTENDANCE_CORRECTION_REQUESTS_TIME_ORDER", "[RequestedClockOutAtUtc] IS NULL OR [RequestedClockInAtUtc] IS NULL OR [RequestedClockOutAtUtc] >= [RequestedClockInAtUtc]");
                    table.CheckConstraint("CK_ATTENDANCE_ATTENDANCE_CORRECTION_REQUESTS_MAX_DURATION", "[RequestedClockOutAtUtc] IS NULL OR [RequestedClockInAtUtc] IS NULL OR [RequestedClockOutAtUtc] <= DATEADD(hour, 24, [RequestedClockInAtUtc])");
                    table.ForeignKey(
                        name: "FK_ATTENDANCE_ATTENDANCE_CORRECTION_REQUESTS_ATTENDANCE_COMPANY_MEMBERSHIPS_CompanyId_DirectApproverMembershipId",
                        columns: x => new { x.CompanyId, x.DirectApproverMembershipId },
                        principalTable: "ATTENDANCE_COMPANY_MEMBERSHIPS",
                        principalColumns: new[] { "CompanyId", "MembershipId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ATTENDANCE_ATTENDANCE_CORRECTION_REQUESTS_ATTENDANCE_COMPANY_MEMBERSHIPS_CompanyId_RequestedByMembershipId",
                        columns: x => new { x.CompanyId, x.RequestedByMembershipId },
                        principalTable: "ATTENDANCE_COMPANY_MEMBERSHIPS",
                        principalColumns: new[] { "CompanyId", "MembershipId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ATTENDANCE_ATTENDANCE_CORRECTION_REQUESTS_ATTENDANCE_COMPANY_MEMBERSHIPS_CompanyId_ReviewedByMembershipId",
                        columns: x => new { x.CompanyId, x.ReviewedByMembershipId },
                        principalTable: "ATTENDANCE_COMPANY_MEMBERSHIPS",
                        principalColumns: new[] { "CompanyId", "MembershipId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ATTENDANCE_ATTENDANCE_CORRECTION_REQUESTS_ATTENDANCE_EMPLOYEE_PROFILES_CompanyId_EmployeeProfileId",
                        columns: x => new { x.CompanyId, x.EmployeeProfileId },
                        principalTable: "ATTENDANCE_EMPLOYEE_PROFILES",
                        principalColumns: new[] { "CompanyId", "EmployeeProfileId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ATTENDANCE_ATTENDANCE_CORRECTION_REQUESTS_ATTENDANCE_WORK_SHIFTS_CompanyId_WorkShiftId_EmployeeProfileId",
                        columns: x => new { x.CompanyId, x.WorkShiftId, x.EmployeeProfileId },
                        principalTable: "ATTENDANCE_WORK_SHIFTS",
                        principalColumns: new[] { "CompanyId", "WorkShiftId", "EmployeeProfileId" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ATTENDANCE_EMPLOYEE_MONTHLY_WORK_SCHEDULES",
                columns: table => new
                {
                    ScheduleId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EmployeeProfileId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Year = table.Column<int>(type: "int", nullable: false),
                    Month = table.Column<int>(type: "int", nullable: false),
                    ConfiguredByMembershipId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset(7)", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset(7)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ATTENDANCE_EMPLOYEE_MONTHLY_WORK_SCHEDULES", x => x.ScheduleId);
                    table.UniqueConstraint("AK_ATTENDANCE_EMPLOYEE_MONTHLY_WORK_SCHEDULES_CompanyId_ScheduleId", x => new { x.CompanyId, x.ScheduleId });
                    table.CheckConstraint("CK_ATTENDANCE_EMPLOYEE_MONTHLY_WORK_SCHEDULES_MONTH", "[Month] >= 1 AND [Month] <= 12");
                    table.CheckConstraint("CK_ATTENDANCE_EMPLOYEE_MONTHLY_WORK_SCHEDULES_YEAR", "[Year] >= 2000 AND [Year] <= 9999");
                    table.ForeignKey(
                        name: "FK_ATTENDANCE_EMPLOYEE_MONTHLY_WORK_SCHEDULES_ATTENDANCE_COMPANY_MEMBERSHIPS_CompanyId_ConfiguredByMembershipId",
                        columns: x => new { x.CompanyId, x.ConfiguredByMembershipId },
                        principalTable: "ATTENDANCE_COMPANY_MEMBERSHIPS",
                        principalColumns: new[] { "CompanyId", "MembershipId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ATTENDANCE_EMPLOYEE_MONTHLY_WORK_SCHEDULES_ATTENDANCE_EMPLOYEE_PROFILES_CompanyId_EmployeeProfileId",
                        columns: x => new { x.CompanyId, x.EmployeeProfileId },
                        principalTable: "ATTENDANCE_EMPLOYEE_PROFILES",
                        principalColumns: new[] { "CompanyId", "EmployeeProfileId" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ATTENDANCE_EMPLOYEE_MONTHLY_WORK_DAYS",
                columns: table => new
                {
                    WorkDayId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ScheduleId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    WorkDate = table.Column<DateOnly>(type: "date", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ATTENDANCE_EMPLOYEE_MONTHLY_WORK_DAYS", x => x.WorkDayId);
                    table.ForeignKey(
                        name: "FK_ATTENDANCE_EMPLOYEE_MONTHLY_WORK_DAYS_ATTENDANCE_EMPLOYEE_MONTHLY_WORK_SCHEDULES_CompanyId_ScheduleId",
                        columns: x => new { x.CompanyId, x.ScheduleId },
                        principalTable: "ATTENDANCE_EMPLOYEE_MONTHLY_WORK_SCHEDULES",
                        principalColumns: new[] { "CompanyId", "ScheduleId" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.AddCheckConstraint(
                name: "CK_ATTENDANCE_WORK_SHIFTS_CLOCK_IN_SOURCE",
                table: "ATTENDANCE_WORK_SHIFTS",
                sql: "([ClockInSource] = 'ExternalApi' AND [ClockInProvider] IS NOT NULL AND [ClockInProviderSampleUtc] IS NOT NULL) OR ([ClockInSource] = 'ManagerApprovedCorrection' AND [ClockInProvider] IS NULL AND [ClockInProviderSampleUtc] IS NULL)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_ATTENDANCE_WORK_SHIFTS_CLOCK_OUT_SOURCE",
                table: "ATTENDANCE_WORK_SHIFTS",
                sql: "([ClockOutAtUtc] IS NULL AND [ClockOutSource] IS NULL AND [ClockOutProvider] IS NULL AND [ClockOutProviderSampleUtc] IS NULL) OR ([ClockOutAtUtc] IS NOT NULL AND [ClockOutSource] = 'ExternalApi' AND [ClockOutProvider] IS NOT NULL AND [ClockOutProviderSampleUtc] IS NOT NULL) OR ([ClockOutAtUtc] IS NOT NULL AND [ClockOutSource] = 'ManagerApprovedCorrection' AND [ClockOutProvider] IS NULL AND [ClockOutProviderSampleUtc] IS NULL)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_ATTENDANCE_WORK_SHIFTS_MAX_DURATION",
                table: "ATTENDANCE_WORK_SHIFTS",
                sql: "[ClockOutAtUtc] IS NULL OR [ClockOutAtUtc] <= DATEADD(hour, 24, [ClockInAtUtc])");

            migrationBuilder.CreateIndex(
                name: "IX_ATTENDANCE_ATTENDANCE_CORRECTION_REQUESTS_CompanyId_DirectApproverMembershipId_Status",
                table: "ATTENDANCE_ATTENDANCE_CORRECTION_REQUESTS",
                columns: new[] { "CompanyId", "DirectApproverMembershipId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_ATTENDANCE_ATTENDANCE_CORRECTION_REQUESTS_CompanyId_EmployeeProfileId_WorkDate",
                table: "ATTENDANCE_ATTENDANCE_CORRECTION_REQUESTS",
                columns: new[] { "CompanyId", "EmployeeProfileId", "WorkDate" },
                unique: true,
                filter: "[Status] = 'PendingApproval'");

            migrationBuilder.CreateIndex(
                name: "IX_ATTENDANCE_ATTENDANCE_CORRECTION_REQUESTS_CompanyId_RequestedByMembershipId",
                table: "ATTENDANCE_ATTENDANCE_CORRECTION_REQUESTS",
                columns: new[] { "CompanyId", "RequestedByMembershipId" });

            migrationBuilder.CreateIndex(
                name: "IX_ATTENDANCE_ATTENDANCE_CORRECTION_REQUESTS_CompanyId_ReviewedByMembershipId",
                table: "ATTENDANCE_ATTENDANCE_CORRECTION_REQUESTS",
                columns: new[] { "CompanyId", "ReviewedByMembershipId" });

            migrationBuilder.CreateIndex(
                name: "IX_ATTENDANCE_ATTENDANCE_CORRECTION_REQUESTS_CompanyId_Status_SubmittedAtUtc",
                table: "ATTENDANCE_ATTENDANCE_CORRECTION_REQUESTS",
                columns: new[] { "CompanyId", "Status", "SubmittedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_ATTENDANCE_ATTENDANCE_CORRECTION_REQUESTS_CompanyId_WorkShiftId_EmployeeProfileId",
                table: "ATTENDANCE_ATTENDANCE_CORRECTION_REQUESTS",
                columns: new[] { "CompanyId", "WorkShiftId", "EmployeeProfileId" });

            migrationBuilder.CreateIndex(
                name: "IX_ATTENDANCE_EMPLOYEE_MONTHLY_WORK_DAYS_CompanyId_ScheduleId_WorkDate",
                table: "ATTENDANCE_EMPLOYEE_MONTHLY_WORK_DAYS",
                columns: new[] { "CompanyId", "ScheduleId", "WorkDate" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ATTENDANCE_EMPLOYEE_MONTHLY_WORK_SCHEDULES_CompanyId_ConfiguredByMembershipId",
                table: "ATTENDANCE_EMPLOYEE_MONTHLY_WORK_SCHEDULES",
                columns: new[] { "CompanyId", "ConfiguredByMembershipId" });

            migrationBuilder.CreateIndex(
                name: "IX_ATTENDANCE_EMPLOYEE_MONTHLY_WORK_SCHEDULES_CompanyId_EmployeeProfileId_Year_Month",
                table: "ATTENDANCE_EMPLOYEE_MONTHLY_WORK_SCHEDULES",
                columns: new[] { "CompanyId", "EmployeeProfileId", "Year", "Month" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ATTENDANCE_ATTENDANCE_CORRECTION_REQUESTS");

            migrationBuilder.DropTable(
                name: "ATTENDANCE_EMPLOYEE_MONTHLY_WORK_DAYS");

            migrationBuilder.DropTable(
                name: "ATTENDANCE_EMPLOYEE_MONTHLY_WORK_SCHEDULES");

            migrationBuilder.DropCheckConstraint(
                name: "CK_ATTENDANCE_WORK_SHIFTS_CLOCK_IN_SOURCE",
                table: "ATTENDANCE_WORK_SHIFTS");

            migrationBuilder.DropCheckConstraint(
                name: "CK_ATTENDANCE_WORK_SHIFTS_CLOCK_OUT_SOURCE",
                table: "ATTENDANCE_WORK_SHIFTS");

            migrationBuilder.DropCheckConstraint(
                name: "CK_ATTENDANCE_WORK_SHIFTS_MAX_DURATION",
                table: "ATTENDANCE_WORK_SHIFTS");

            migrationBuilder.DropColumn(
                name: "ClockInSource",
                table: "ATTENDANCE_WORK_SHIFTS");

            migrationBuilder.DropColumn(
                name: "ClockOutSource",
                table: "ATTENDANCE_WORK_SHIFTS");

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "ClockInProviderSampleUtc",
                table: "ATTENDANCE_WORK_SHIFTS",
                type: "datetimeoffset(7)",
                nullable: false,
                defaultValue: new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)),
                oldClrType: typeof(DateTimeOffset),
                oldType: "datetimeoffset(7)",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "ClockInProvider",
                table: "ATTENDANCE_WORK_SHIFTS",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "nvarchar(100)",
                oldMaxLength: 100,
                oldNullable: true);
        }
    }
}
