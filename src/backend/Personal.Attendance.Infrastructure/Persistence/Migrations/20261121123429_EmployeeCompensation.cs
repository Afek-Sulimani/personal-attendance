using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Personal.Attendance.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class EmployeeCompensation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CompensationType",
                table: "ATTENDANCE_EMPLOYEE_PROFILES",
                type: "nvarchar(16)",
                maxLength: 16,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "HourlyRate",
                table: "ATTENDANCE_EMPLOYEE_PROFILES",
                type: "decimal(18,2)",
                nullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_ATTENDANCE_EMPLOYEE_PROFILES_COMPENSATION",
                table: "ATTENDANCE_EMPLOYEE_PROFILES",
                sql: "([CompensationType] IS NULL AND [HourlyRate] IS NULL) OR ([CompensationType] = N'Global' AND [HourlyRate] IS NULL) OR ([CompensationType] = N'Hourly' AND [HourlyRate] IS NOT NULL AND [HourlyRate] > 0)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_ATTENDANCE_EMPLOYEE_PROFILES_COMPENSATION",
                table: "ATTENDANCE_EMPLOYEE_PROFILES");

            migrationBuilder.DropColumn(
                name: "CompensationType",
                table: "ATTENDANCE_EMPLOYEE_PROFILES");

            migrationBuilder.DropColumn(
                name: "HourlyRate",
                table: "ATTENDANCE_EMPLOYEE_PROFILES");
        }
    }
}
