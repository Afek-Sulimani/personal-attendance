using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Personal.Attendance.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SequentialEmployeeNumbers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "NextEmployeeNumber",
                table: "ATTENDANCE_COMPANIES",
                type: "int",
                nullable: false,
                defaultValue: 1);

            // Existing profiles receive numbers in membership creation order, with the first company representative first.
            migrationBuilder.Sql("DROP INDEX [IX_ATTENDANCE_EMPLOYEE_PROFILES_CompanyId_EmployeeNumber] ON [ATTENDANCE_EMPLOYEE_PROFILES];");
            migrationBuilder.Sql("""
                IF EXISTS (SELECT 1 FROM ATTENDANCE_EMPLOYEE_PROFILES GROUP BY CompanyId HAVING COUNT(*) > 99999)
                    THROW 51001, 'A company exceeds the five-digit employee number limit.', 1;

                WITH NumberedProfiles AS (
                    SELECT profile.EmployeeProfileId,
                           ROW_NUMBER() OVER (
                               PARTITION BY profile.CompanyId
                               ORDER BY CASE WHEN membership.ManagerMembershipId IS NULL THEN 0 ELSE 1 END,
                                        membership.CreatedAtUtc, profile.EmployeeProfileId
                           ) AS SequenceNumber
                    FROM ATTENDANCE_EMPLOYEE_PROFILES AS profile
                    JOIN ATTENDANCE_COMPANY_MEMBERSHIPS AS membership ON membership.MembershipId = profile.MembershipId
                )
                UPDATE profile
                SET EmployeeNumber = RIGHT('00000' + CONVERT(varchar(5), numbered.SequenceNumber), 5)
                FROM ATTENDANCE_EMPLOYEE_PROFILES AS profile
                JOIN NumberedProfiles AS numbered ON numbered.EmployeeProfileId = profile.EmployeeProfileId;

                UPDATE company SET NextEmployeeNumber = counts.ProfileCount + 1
                FROM ATTENDANCE_COMPANIES AS company
                JOIN (SELECT CompanyId, COUNT(*) AS ProfileCount FROM ATTENDANCE_EMPLOYEE_PROFILES GROUP BY CompanyId) AS counts
                  ON counts.CompanyId = company.CompanyId;

                DELETE role FROM ATTENDANCE_MEMBERSHIP_ROLES AS role
                WHERE role.Role = N'Employee'
                  AND EXISTS (SELECT 1 FROM ATTENDANCE_MEMBERSHIP_ROLES AS other
                              WHERE other.MembershipId = role.MembershipId AND other.Role <> N'Employee');
                """);
            migrationBuilder.Sql("CREATE UNIQUE INDEX [IX_ATTENDANCE_EMPLOYEE_PROFILES_CompanyId_EmployeeNumber] ON [ATTENDANCE_EMPLOYEE_PROFILES] ([CompanyId], [EmployeeNumber]) WHERE [EmployeeNumber] IS NOT NULL;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "NextEmployeeNumber",
                table: "ATTENDANCE_COMPANIES");
        }
    }
}
