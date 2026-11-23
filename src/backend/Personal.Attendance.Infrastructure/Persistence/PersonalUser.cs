using Microsoft.AspNetCore.Identity;

namespace Personal.Attendance.Infrastructure.Persistence;

public sealed class PersonalUser : IdentityUser<Guid>
{
}
