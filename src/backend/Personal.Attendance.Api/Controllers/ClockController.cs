using Microsoft.AspNetCore.Mvc;
using Personal.Attendance.Application.Time;

namespace Personal.Attendance.Api.Controllers;

[ApiController]
[Route("api/clock")]
public sealed class ClockController(
    ICurrentAttendanceClock currentAttendanceClock,
    ICompanyTimeZoneRegistry timeZoneRegistry) : ControllerBase
{
    [HttpGet("current")]
    [ProducesResponseType<ClockReading>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<ClockReading>> GetCurrentTime(
        [FromQuery] string? timeZoneId,
        CancellationToken cancellationToken)
    {
        try
        {
            var selectedTimeZoneId = string.IsNullOrWhiteSpace(timeZoneId)
                ? timeZoneRegistry.DefaultTimeZoneId
                : timeZoneRegistry.Register(timeZoneId);
            return Ok(await currentAttendanceClock.GetCurrentTimeAsync(selectedTimeZoneId, cancellationToken));
        }
        catch (TimeZoneNotFoundException)
        {
            return BadRequest(new { error = "The requested time zone is not supported." });
        }
        catch (InvalidTimeZoneException)
        {
            return BadRequest(new { error = "The requested time zone is invalid." });
        }
        catch (ClockUnavailableException)
        {
            return StatusCode(
                StatusCodes.Status503ServiceUnavailable,
                new { error = "The requested time zone's clock is temporarily unavailable." });
        }
    }
}
