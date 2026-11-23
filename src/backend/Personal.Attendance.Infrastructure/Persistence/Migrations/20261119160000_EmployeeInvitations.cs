using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Personal.Attendance.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class EmployeeInvitations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ATTENDANCE_EMPLOYEE_INVITATIONS",
                columns: table => new
                {
                    InvitationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedByMembershipId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ManagerMembershipId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    InvitedEmail = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    NormalizedEmail = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    DisplayName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    EmployeeNumber = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    AssignedRoles = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    TokenHash = table.Column<string>(type: "char(64)", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset(7)", nullable: false),
                    ExpiresAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset(7)", nullable: false),
                    AcceptedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset(7)", nullable: true),
                    AcceptedUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RevokedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset(7)", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ATTENDANCE_EMPLOYEE_INVITATIONS", x => x.InvitationId);
                    table.CheckConstraint("CK_ATTENDANCE_EMPLOYEE_INVITATIONS_Expiration", "[ExpiresAtUtc] > [CreatedAtUtc]");
                    table.ForeignKey(
                        name: "FK_ATTENDANCE_EMPLOYEE_INVITATIONS_ATTENDANCE_COMPANIES_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "ATTENDANCE_COMPANIES",
                        principalColumn: "CompanyId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ATTENDANCE_EMPLOYEE_INVITATIONS_ATTENDANCE_COMPANY_MEMBERSHIPS_CompanyId_CreatedByMembershipId",
                        columns: x => new { x.CompanyId, x.CreatedByMembershipId },
                        principalTable: "ATTENDANCE_COMPANY_MEMBERSHIPS",
                        principalColumns: new[] { "CompanyId", "MembershipId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ATTENDANCE_EMPLOYEE_INVITATIONS_ATTENDANCE_COMPANY_MEMBERSHIPS_CompanyId_ManagerMembershipId",
                        columns: x => new { x.CompanyId, x.ManagerMembershipId },
                        principalTable: "ATTENDANCE_COMPANY_MEMBERSHIPS",
                        principalColumns: new[] { "CompanyId", "MembershipId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ATTENDANCE_EMPLOYEE_INVITATIONS_ATTENDANCE_USERS_AcceptedUserId",
                        column: x => x.AcceptedUserId,
                        principalTable: "ATTENDANCE_USERS",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ATTENDANCE_EMPLOYEE_INVITATIONS_AcceptedUserId",
                table: "ATTENDANCE_EMPLOYEE_INVITATIONS",
                column: "AcceptedUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ATTENDANCE_EMPLOYEE_INVITATIONS_CompanyId_CreatedByMembershipId",
                table: "ATTENDANCE_EMPLOYEE_INVITATIONS",
                columns: new[] { "CompanyId", "CreatedByMembershipId" });

            migrationBuilder.CreateIndex(
                name: "IX_ATTENDANCE_EMPLOYEE_INVITATIONS_CompanyId_ManagerMembershipId",
                table: "ATTENDANCE_EMPLOYEE_INVITATIONS",
                columns: new[] { "CompanyId", "ManagerMembershipId" });

            migrationBuilder.CreateIndex(
                name: "IX_ATTENDANCE_EMPLOYEE_INVITATIONS_CompanyId_NormalizedEmail",
                table: "ATTENDANCE_EMPLOYEE_INVITATIONS",
                columns: new[] { "CompanyId", "NormalizedEmail" });

            migrationBuilder.CreateIndex(
                name: "IX_ATTENDANCE_EMPLOYEE_INVITATIONS_TokenHash",
                table: "ATTENDANCE_EMPLOYEE_INVITATIONS",
                column: "TokenHash",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ATTENDANCE_EMPLOYEE_INVITATIONS");
        }
    }
}
