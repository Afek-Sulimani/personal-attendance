using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Personal.Attendance.Domain.Entities;

namespace Personal.Attendance.Infrastructure.Persistence;

public sealed class PersonalAttendanceDbContext(
    DbContextOptions<PersonalAttendanceDbContext> options)
    : IdentityDbContext<PersonalUser, PersonalRole, Guid>(options)
{
    public DbSet<Company> Companies => Set<Company>();
    public DbSet<CompanyMembership> CompanyMemberships => Set<CompanyMembership>();
    public DbSet<MembershipRole> MembershipRoles => Set<MembershipRole>();
    public DbSet<EmployeeProfile> EmployeeProfiles => Set<EmployeeProfile>();
    public DbSet<EmployeeInvitation> EmployeeInvitations => Set<EmployeeInvitation>();
    public DbSet<EmployeeMonthlyWorkSchedule> EmployeeMonthlyWorkSchedules => Set<EmployeeMonthlyWorkSchedule>();
    public DbSet<EmployeeMonthlyWorkDay> EmployeeMonthlyWorkDays => Set<EmployeeMonthlyWorkDay>();
    public DbSet<AttendanceCorrectionRequest> AttendanceCorrectionRequests => Set<AttendanceCorrectionRequest>();
    public DbSet<CompanyPayrollSettings> CompanyPayrollSettings => Set<CompanyPayrollSettings>();
    public DbSet<CompanyDefaultWorkDay> CompanyDefaultWorkDays => Set<CompanyDefaultWorkDay>();
    public DbSet<AdminAttendanceChangeLog> AdminAttendanceChangeLogs => Set<AdminAttendanceChangeLog>();
    public DbSet<WorkShift> WorkShifts => Set<WorkShift>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        var users = builder.Entity<PersonalUser>();
        users.ToTable("ATTENDANCE_USERS");
        users.Ignore(user => user.LockoutEnabled);
        users.Ignore(user => user.LockoutEnd);
        users.Ignore(user => user.AccessFailedCount);
        users.Property(user => user.UserName).HasMaxLength(256).IsRequired();
        users.Property(user => user.NormalizedUserName).HasMaxLength(256).IsRequired();
        users.Property(user => user.Email).HasMaxLength(256).IsRequired();
        users.Property(user => user.NormalizedEmail).HasMaxLength(256).IsRequired();
        users.HasIndex(user => user.NormalizedEmail).IsUnique();

        var roles = builder.Entity<PersonalRole>();
        roles.ToTable("ATTENDANCE_ROLES");
        roles.Property(role => role.Name).HasMaxLength(256).IsRequired();
        roles.Property(role => role.NormalizedName).HasMaxLength(256).IsRequired();
        builder.Entity<IdentityUserRole<Guid>>().ToTable("ATTENDANCE_USER_ROLES");
        builder.Entity<IdentityUserClaim<Guid>>().ToTable("ATTENDANCE_USER_CLAIMS");
        builder.Entity<IdentityUserLogin<Guid>>().ToTable("ATTENDANCE_USER_LOGINS");
        builder.Entity<IdentityRoleClaim<Guid>>().ToTable("ATTENDANCE_ROLE_CLAIMS");
        builder.Entity<IdentityUserToken<Guid>>().ToTable("ATTENDANCE_USER_TOKENS");

        builder.ApplyConfigurationsFromAssembly(typeof(PersonalAttendanceDbContext).Assembly);
    }
}
