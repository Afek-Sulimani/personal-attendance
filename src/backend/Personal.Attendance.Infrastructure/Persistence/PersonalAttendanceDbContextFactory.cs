using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Personal.Attendance.Infrastructure.Persistence;

public sealed class PersonalAttendanceDbContextFactory
    : IDesignTimeDbContextFactory<PersonalAttendanceDbContext>
{
    public PersonalAttendanceDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__Attendance");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            connectionString =
                "Server=localhost;Database=PersonalAttendance_DesignTime;Integrated Security=True;TrustServerCertificate=True";
        }

        var options = new DbContextOptionsBuilder<PersonalAttendanceDbContext>()
            .UseSqlServer(connectionString)
            .Options;

        return new PersonalAttendanceDbContext(options);
    }
}
