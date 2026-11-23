using Microsoft.AspNetCore.Mvc;

namespace Personal.Attendance.Api.Controllers;

[ApiController]
[Route("api/health")]
public sealed class HealthController : ControllerBase
{
    [HttpGet("live")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public IActionResult Live()
    {
        return Ok(new
        {
            status = "ok",
            application = "Personal.Attendance.Api",
            stage = "scaffold"
        });
    }
}
