using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Personal.Attendance.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RemoveLegacyMonthlyReportEntity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ATTENDANCE_MONTHLY_ATTENDANCE_REPORTS");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ATTENDANCE_MONTHLY_ATTENDANCE_REPORTS",
                columns: table => new
                {
                    ReportId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EmployeeProfileId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DecisionReason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    DirectApproverMembershipId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    PeriodEnd = table.Column<DateOnly>(type: "date", nullable: false),
                    PeriodStart = table.Column<DateOnly>(type: "date", nullable: false),
                    ReviewedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset(7)", nullable: true),
                    ReviewedByMembershipId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    SubmittedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset(7)", nullable: false),
                    TimeZoneId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ATTENDANCE_MONTHLY_ATTENDANCE_REPORTS", x => x.ReportId);
                    table.UniqueConstraint("AK_ATTENDANCE_MONTHLY_ATTENDANCE_REPORTS_CompanyId_ReportId", x => new { x.CompanyId, x.ReportId });
                    table.ForeignKey(
                        name: "FK_ATTENDANCE_MONTHLY_ATTENDANCE_REPORTS_ATTENDANCE_COMPANY_MEMBERSHIPS_CompanyId_DirectApproverMembershipId",
                        columns: x => new { x.CompanyId, x.DirectApproverMembershipId },
                        principalTable: "ATTENDANCE_COMPANY_MEMBERSHIPS",
                        principalColumns: new[] { "CompanyId", "MembershipId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ATTENDANCE_MONTHLY_ATTENDANCE_REPORTS_ATTENDANCE_COMPANY_MEMBERSHIPS_CompanyId_ReviewedByMembershipId",
                        columns: x => new { x.CompanyId, x.ReviewedByMembershipId },
                        principalTable: "ATTENDANCE_COMPANY_MEMBERSHIPS",
                        principalColumns: new[] { "CompanyId", "MembershipId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ATTENDANCE_MONTHLY_ATTENDANCE_REPORTS_ATTENDANCE_EMPLOYEE_PROFILES_CompanyId_EmployeeProfileId",
                        columns: x => new { x.CompanyId, x.EmployeeProfileId },
                        principalTable: "ATTENDANCE_EMPLOYEE_PROFILES",
                        principalColumns: new[] { "CompanyId", "EmployeeProfileId" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ATTENDANCE_MONTHLY_ATTENDANCE_REPORTS_CompanyId_DirectApproverMembershipId_Status",
                table: "ATTENDANCE_MONTHLY_ATTENDANCE_REPORTS",
                columns: new[] { "CompanyId", "DirectApproverMembershipId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_ATTENDANCE_MONTHLY_ATTENDANCE_REPORTS_CompanyId_EmployeeProfileId_PeriodStart",
                table: "ATTENDANCE_MONTHLY_ATTENDANCE_REPORTS",
                columns: new[] { "CompanyId", "EmployeeProfileId", "PeriodStart" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ATTENDANCE_MONTHLY_ATTENDANCE_REPORTS_CompanyId_ReviewedByMembershipId",
                table: "ATTENDANCE_MONTHLY_ATTENDANCE_REPORTS",
                columns: new[] { "CompanyId", "ReviewedByMembershipId" });
        }
    }
}
