namespace Personal.Attendance.Application.Time;

public sealed class ClockUnavailableException : Exception
{
    public ClockUnavailableException()
        : base("A fresh external time sample for the requested time zone is unavailable.")
    {
    }
}
