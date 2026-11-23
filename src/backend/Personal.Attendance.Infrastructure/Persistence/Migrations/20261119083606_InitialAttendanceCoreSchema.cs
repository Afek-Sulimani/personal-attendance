using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Personal.Attendance.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialAttendanceCoreSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ATTENDANCE_COMPANIES",
                columns: table => new
                {
                    CompanyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CompanyCode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    NormalizedCompanyCode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset(7)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ATTENDANCE_COMPANIES", x => x.CompanyId);
                });

            migrationBuilder.CreateTable(
                name: "ATTENDANCE_ROLES",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    NormalizedName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    ConcurrencyStamp = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ATTENDANCE_ROLES", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ATTENDANCE_USERS",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    NormalizedUserName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    Email = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    NormalizedEmail = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    EmailConfirmed = table.Column<bool>(type: "bit", nullable: false),
                    PasswordHash = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SecurityStamp = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ConcurrencyStamp = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PhoneNumber = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PhoneNumberConfirmed = table.Column<bool>(type: "bit", nullable: false),
                    TwoFactorEnabled = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ATTENDANCE_USERS", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ATTENDANCE_ROLE_CLAIMS",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RoleId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ClaimType = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ClaimValue = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ATTENDANCE_ROLE_CLAIMS", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ATTENDANCE_ROLE_CLAIMS_ATTENDANCE_ROLES_RoleId",
                        column: x => x.RoleId,
                        principalTable: "ATTENDANCE_ROLES",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ATTENDANCE_COMPANY_MEMBERSHIPS",
                columns: table => new
                {
                    MembershipId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ManagerMembershipId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset(7)", nullable: false),
                    DeactivatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset(7)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ATTENDANCE_COMPANY_MEMBERSHIPS", x => x.MembershipId);
                    table.UniqueConstraint("AK_ATTENDANCE_COMPANY_MEMBERSHIPS_CompanyId_MembershipId", x => new { x.CompanyId, x.MembershipId });
                    table.ForeignKey(
                        name: "FK_ATTENDANCE_COMPANY_MEMBERSHIPS_ATTENDANCE_COMPANIES_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "ATTENDANCE_COMPANIES",
                        principalColumn: "CompanyId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ATTENDANCE_COMPANY_MEMBERSHIPS_ATTENDANCE_COMPANY_MEMBERSHIPS_CompanyId_ManagerMembershipId",
                        columns: x => new { x.CompanyId, x.ManagerMembershipId },
                        principalTable: "ATTENDANCE_COMPANY_MEMBERSHIPS",
                        principalColumns: new[] { "CompanyId", "MembershipId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ATTENDANCE_COMPANY_MEMBERSHIPS_ATTENDANCE_USERS_UserId",
                        column: x => x.UserId,
                        principalTable: "ATTENDANCE_USERS",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ATTENDANCE_USER_CLAIMS",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ClaimType = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ClaimValue = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ATTENDANCE_USER_CLAIMS", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ATTENDANCE_USER_CLAIMS_ATTENDANCE_USERS_UserId",
                        column: x => x.UserId,
                        principalTable: "ATTENDANCE_USERS",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ATTENDANCE_USER_LOGINS",
                columns: table => new
                {
                    LoginProvider = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    ProviderKey = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    ProviderDisplayName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ATTENDANCE_USER_LOGINS", x => new { x.LoginProvider, x.ProviderKey });
                    table.ForeignKey(
                        name: "FK_ATTENDANCE_USER_LOGINS_ATTENDANCE_USERS_UserId",
                        column: x => x.UserId,
                        principalTable: "ATTENDANCE_USERS",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ATTENDANCE_USER_ROLES",
                columns: table => new
                {
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RoleId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ATTENDANCE_USER_ROLES", x => new { x.UserId, x.RoleId });
                    table.ForeignKey(
                        name: "FK_ATTENDANCE_USER_ROLES_ATTENDANCE_ROLES_RoleId",
                        column: x => x.RoleId,
                        principalTable: "ATTENDANCE_ROLES",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ATTENDANCE_USER_ROLES_ATTENDANCE_USERS_UserId",
                        column: x => x.UserId,
                        principalTable: "ATTENDANCE_USERS",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ATTENDANCE_USER_TOKENS",
                columns: table => new
                {
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LoginProvider = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Value = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ATTENDANCE_USER_TOKENS", x => new { x.UserId, x.LoginProvider, x.Name });
                    table.ForeignKey(
                        name: "FK_ATTENDANCE_USER_TOKENS_ATTENDANCE_USERS_UserId",
                        column: x => x.UserId,
                        principalTable: "ATTENDANCE_USERS",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ATTENDANCE_EMPLOYEE_PROFILES",
                columns: table => new
                {
                    EmployeeProfileId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MembershipId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DisplayName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    EmployeeNumber = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset(7)", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset(7)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ATTENDANCE_EMPLOYEE_PROFILES", x => x.EmployeeProfileId);
                    table.UniqueConstraint("AK_ATTENDANCE_EMPLOYEE_PROFILES_CompanyId_EmployeeProfileId", x => new { x.CompanyId, x.EmployeeProfileId });
                    table.ForeignKey(
                        name: "FK_ATTENDANCE_EMPLOYEE_PROFILES_ATTENDANCE_COMPANY_MEMBERSHIPS_CompanyId_MembershipId",
                        columns: x => new { x.CompanyId, x.MembershipId },
                        principalTable: "ATTENDANCE_COMPANY_MEMBERSHIPS",
                        principalColumns: new[] { "CompanyId", "MembershipId" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ATTENDANCE_MEMBERSHIP_ROLES",
                columns: table => new
                {
                    MembershipId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Role = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ATTENDANCE_MEMBERSHIP_ROLES", x => new { x.MembershipId, x.Role });
                    table.ForeignKey(
                        name: "FK_ATTENDANCE_MEMBERSHIP_ROLES_ATTENDANCE_COMPANY_MEMBERSHIPS_MembershipId",
                        column: x => x.MembershipId,
                        principalTable: "ATTENDANCE_COMPANY_MEMBERSHIPS",
                        principalColumn: "MembershipId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ATTENDANCE_WORK_SHIFTS",
                columns: table => new
                {
                    WorkShiftId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EmployeeProfileId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ClockInAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset(7)", nullable: false),
                    ClockInProvider = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ClockInProviderSampleUtc = table.Column<DateTimeOffset>(type: "datetimeoffset(7)", nullable: false),
                    ClockOutAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset(7)", nullable: true),
                    ClockOutProvider = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ClockOutProviderSampleUtc = table.Column<DateTimeOffset>(type: "datetimeoffset(7)", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ATTENDANCE_WORK_SHIFTS", x => x.WorkShiftId);
                    table.UniqueConstraint("AK_ATTENDANCE_WORK_SHIFTS_CompanyId_WorkShiftId", x => new { x.CompanyId, x.WorkShiftId });
                    table.UniqueConstraint("AK_ATTENDANCE_WORK_SHIFTS_CompanyId_WorkShiftId_EmployeeProfileId", x => new { x.CompanyId, x.WorkShiftId, x.EmployeeProfileId });
                    table.CheckConstraint("CK_ATTENDANCE_WORK_SHIFTS_CLOCK_ORDER", "[ClockOutAtUtc] IS NULL OR [ClockOutAtUtc] >= [ClockInAtUtc]");
                    table.ForeignKey(
                        name: "FK_ATTENDANCE_WORK_SHIFTS_ATTENDANCE_EMPLOYEE_PROFILES_CompanyId_EmployeeProfileId",
                        columns: x => new { x.CompanyId, x.EmployeeProfileId },
                        principalTable: "ATTENDANCE_EMPLOYEE_PROFILES",
                        principalColumns: new[] { "CompanyId", "EmployeeProfileId" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ATTENDANCE_COMPANIES_NormalizedCompanyCode",
                table: "ATTENDANCE_COMPANIES",
                column: "NormalizedCompanyCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ATTENDANCE_COMPANY_MEMBERSHIPS_CompanyId_ManagerMembershipId",
                table: "ATTENDANCE_COMPANY_MEMBERSHIPS",
                columns: new[] { "CompanyId", "ManagerMembershipId" });

            migrationBuilder.CreateIndex(
                name: "IX_ATTENDANCE_COMPANY_MEMBERSHIPS_CompanyId_UserId",
                table: "ATTENDANCE_COMPANY_MEMBERSHIPS",
                columns: new[] { "CompanyId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ATTENDANCE_COMPANY_MEMBERSHIPS_UserId",
                table: "ATTENDANCE_COMPANY_MEMBERSHIPS",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_ATTENDANCE_EMPLOYEE_PROFILES_CompanyId_EmployeeNumber",
                table: "ATTENDANCE_EMPLOYEE_PROFILES",
                columns: new[] { "CompanyId", "EmployeeNumber" },
                unique: true,
                filter: "[EmployeeNumber] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ATTENDANCE_EMPLOYEE_PROFILES_CompanyId_MembershipId",
                table: "ATTENDANCE_EMPLOYEE_PROFILES",
                columns: new[] { "CompanyId", "MembershipId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ATTENDANCE_EMPLOYEE_PROFILES_MembershipId",
                table: "ATTENDANCE_EMPLOYEE_PROFILES",
                column: "MembershipId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ATTENDANCE_ROLE_CLAIMS_RoleId",
                table: "ATTENDANCE_ROLE_CLAIMS",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "RoleNameIndex",
                table: "ATTENDANCE_ROLES",
                column: "NormalizedName",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ATTENDANCE_USER_CLAIMS_UserId",
                table: "ATTENDANCE_USER_CLAIMS",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_ATTENDANCE_USER_LOGINS_UserId",
                table: "ATTENDANCE_USER_LOGINS",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_ATTENDANCE_USER_ROLES_RoleId",
                table: "ATTENDANCE_USER_ROLES",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "EmailIndex",
                table: "ATTENDANCE_USERS",
                column: "NormalizedEmail",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UserNameIndex",
                table: "ATTENDANCE_USERS",
                column: "NormalizedUserName",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ATTENDANCE_WORK_SHIFTS_CompanyId_EmployeeProfileId",
                table: "ATTENDANCE_WORK_SHIFTS",
                columns: new[] { "CompanyId", "EmployeeProfileId" },
                unique: true,
                filter: "[ClockOutAtUtc] IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ATTENDANCE_WORK_SHIFTS_CompanyId_EmployeeProfileId_ClockInAtUtc",
                table: "ATTENDANCE_WORK_SHIFTS",
                columns: new[] { "CompanyId", "EmployeeProfileId", "ClockInAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ATTENDANCE_MEMBERSHIP_ROLES");

            migrationBuilder.DropTable(
                name: "ATTENDANCE_ROLE_CLAIMS");

            migrationBuilder.DropTable(
                name: "ATTENDANCE_USER_CLAIMS");

            migrationBuilder.DropTable(
                name: "ATTENDANCE_USER_LOGINS");

            migrationBuilder.DropTable(
                name: "ATTENDANCE_USER_ROLES");

            migrationBuilder.DropTable(
                name: "ATTENDANCE_USER_TOKENS");

            migrationBuilder.DropTable(
                name: "ATTENDANCE_WORK_SHIFTS");

            migrationBuilder.DropTable(
                name: "ATTENDANCE_ROLES");

            migrationBuilder.DropTable(
                name: "ATTENDANCE_EMPLOYEE_PROFILES");

            migrationBuilder.DropTable(
                name: "ATTENDANCE_COMPANY_MEMBERSHIPS");

            migrationBuilder.DropTable(
                name: "ATTENDANCE_COMPANIES");

            migrationBuilder.DropTable(
                name: "ATTENDANCE_USERS");
        }
    }
}
