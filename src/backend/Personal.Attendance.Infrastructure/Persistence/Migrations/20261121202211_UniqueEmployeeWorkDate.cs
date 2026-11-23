using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Personal.Attendance.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class UniqueEmployeeWorkDate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ATTENDANCE_WORK_SHIFTS_CompanyId_EmployeeProfileId_WorkDate",
                table: "ATTENDANCE_WORK_SHIFTS");

            migrationBuilder.CreateIndex(
                name: "IX_ATTENDANCE_WORK_SHIFTS_CompanyId_EmployeeProfileId_WorkDate",
                table: "ATTENDANCE_WORK_SHIFTS",
                columns: new[] { "CompanyId", "EmployeeProfileId", "WorkDate" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ATTENDANCE_WORK_SHIFTS_CompanyId_EmployeeProfileId_WorkDate",
                table: "ATTENDANCE_WORK_SHIFTS");

            migrationBuilder.CreateIndex(
                name: "IX_ATTENDANCE_WORK_SHIFTS_CompanyId_EmployeeProfileId_WorkDate",
                table: "ATTENDANCE_WORK_SHIFTS",
                columns: new[] { "CompanyId", "EmployeeProfileId", "WorkDate" });
        }
    }
}
