using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Personal.Attendance.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class CompanySelfRegistration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<Guid>(
                name: "CreatedByMembershipId",
                table: "ATTENDANCE_EMPLOYEE_INVITATIONS",
                type: "uniqueidentifier",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                "IF EXISTS (SELECT 1 FROM [ATTENDANCE_EMPLOYEE_INVITATIONS] WHERE [CreatedByMembershipId] IS NULL) " +
                "BEGIN ;THROW 51000, 'Cannot downgrade while company registration invitations exist.', 1; END;");

            migrationBuilder.AlterColumn<Guid>(
                name: "CreatedByMembershipId",
                table: "ATTENDANCE_EMPLOYEE_INVITATIONS",
                type: "uniqueidentifier",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true);
        }
    }
}
