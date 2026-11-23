using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Personal.Attendance.Application.Time;

namespace Personal.Attendance.Api.Controllers;

[ApiController]
[Route("api/time-sources")]
public sealed class TimeSourcesController(
    IEnumerable<ICurrentTimeSource> timeSources,
    ICompanyTimeZoneRegistry timeZoneRegistry) : ControllerBase
{
    [HttpGet("{sourceId}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<CurrentTimeSample>> GetCurrentTime(
        string sourceId,
        [FromQuery] string? timeZoneId,
        CancellationToken cancellationToken)
    {
        var source = timeSources.FirstOrDefault(candidate =>
            string.Equals(candidate.Id, sourceId, StringComparison.OrdinalIgnoreCase));

        if (source is null)
        {
            return NotFound(new { error = "Unknown time source." });
        }

        try
        {
            var selectedTimeZoneId = string.IsNullOrWhiteSpace(timeZoneId)
                ? timeZoneRegistry.DefaultTimeZoneId
                : timeZoneRegistry.Register(timeZoneId);
            return Ok(await source.GetCurrentTimeAsync(selectedTimeZoneId, cancellationToken));
        }
        catch (TimeZoneNotFoundException)
        {
            return BadRequest(new { error = "The requested time zone is not supported." });
        }
        catch (InvalidTimeZoneException)
        {
            return BadRequest(new { error = "The requested time zone is invalid." });
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return StatusCode(StatusCodes.Status504GatewayTimeout,
                new { error = "The time provider did not respond before the timeout." });
        }
        catch (HttpRequestException)
        {
            return StatusCode(StatusCodes.Status502BadGateway,
                new { error = "The time provider request failed." });
        }
        catch (JsonException)
        {
            return StatusCode(StatusCodes.Status502BadGateway,
                new { error = "The time provider returned invalid JSON." });
        }
        catch (InvalidDataException)
        {
            return StatusCode(StatusCodes.Status502BadGateway,
                new { error = "The time provider returned invalid time data." });
        }
    }
}
