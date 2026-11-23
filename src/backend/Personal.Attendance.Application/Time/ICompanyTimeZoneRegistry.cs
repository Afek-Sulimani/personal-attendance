namespace Personal.Attendance.Application.Time;

public interface ICompanyTimeZoneRegistry
{
    string DefaultTimeZoneId { get; }

    string Register(string timeZoneId);

    IReadOnlyList<string> GetRegisteredTimeZones();
}
