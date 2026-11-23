using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Personal.Attendance.Api.Controllers;

[ApiController]
[Route("api/time-zones")]
[Authorize]
public sealed class TimeZonesController : ControllerBase
{
    [HttpGet]
    public IActionResult Get() => Ok(TimeZoneInfo.GetSystemTimeZones()
        .OrderBy(zone => zone.Id)
        .Select(zone => new { id = zone.Id, label = $"{zone.Id.Replace('_', ' ')} — {zone.DisplayName}" }));
}
