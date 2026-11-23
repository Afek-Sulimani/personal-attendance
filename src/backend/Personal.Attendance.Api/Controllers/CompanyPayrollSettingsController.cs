using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Personal.Attendance.Api.Authentication;
using Personal.Attendance.Application.Payroll;

namespace Personal.Attendance.Api.Controllers;

[ApiController]
[Route("api/payroll/company-settings")]
[Authorize(
    AuthenticationSchemes = AttendanceAuthenticationDefaults.SchemeName,
    Roles = "Admin,PayrollRepresentative")]
public sealed class CompanyPayrollSettingsController(
    ICompanyPayrollSettingsService settingsService,
    ILogger<CompanyPayrollSettingsController> logger) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<CompanyPayrollSettingsResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<CompanyPayrollSettingsErrorResponse>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<CompanyPayrollSettingsErrorResponse>(StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<CompanyPayrollSettingsResponse>> Get(CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actor)) return Unauthorized();

        try
        {
            return Ok(ToResponse(await settingsService.GetAsync(actor, cancellationToken)));
        }
        catch (PayrollAccessDeniedException exception)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new CompanyPayrollSettingsErrorResponse(exception.Message));
        }
        catch (PayrollUnavailableException)
        {
            logger.LogWarning("Company payroll settings could not be loaded.");
            return StatusCode(StatusCodes.Status503ServiceUnavailable,
                new CompanyPayrollSettingsErrorResponse("Payroll settings are temporarily unavailable."));
        }
    }

    [HttpPut]
    [ValidateAntiForgeryToken]
    [ProducesResponseType<CompanyPayrollSettingsResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<CompanyPayrollSettingsErrorResponse>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<CompanyPayrollSettingsErrorResponse>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<CompanyPayrollSettingsErrorResponse>(StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<CompanyPayrollSettingsResponse>> Save(
        SaveCompanyPayrollSettingsRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actor)) return Unauthorized();
        if (!TryParseWorkDays(request.DefaultWorkDays, out var defaultWorkDays))
        {
            return BadRequest(new CompanyPayrollSettingsErrorResponse("Select at least one valid weekday."));
        }

        try
        {
            var settings = await settingsService.SaveAsync(
                actor,
                request.PayrollPeriodStartDay,
                request.TimeZoneId,
                request.DefaultWorkStartTime!.Value,
                request.DefaultWorkEndTime!.Value,
                defaultWorkDays,
                cancellationToken);
            return Ok(ToResponse(settings));
        }
        catch (PayrollAccessDeniedException exception)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new CompanyPayrollSettingsErrorResponse(exception.Message));
        }
        catch (ArgumentOutOfRangeException exception)
        {
            return BadRequest(new CompanyPayrollSettingsErrorResponse(exception.Message));
        }
        catch (ArgumentException exception)
        {
            return BadRequest(new CompanyPayrollSettingsErrorResponse(exception.Message));
        }
        catch (TimeZoneNotFoundException)
        {
            return BadRequest(new CompanyPayrollSettingsErrorResponse("Use a supported time zone ID, such as Asia/Jerusalem."));
        }
        catch (InvalidTimeZoneException)
        {
            return BadRequest(new CompanyPayrollSettingsErrorResponse("The selected time zone definition is invalid."));
        }
        catch (PayrollUnavailableException)
        {
            logger.LogWarning("Company payroll settings could not be saved.");
            return StatusCode(StatusCodes.Status503ServiceUnavailable,
                new CompanyPayrollSettingsErrorResponse("Payroll settings are temporarily unavailable."));
        }
    }

    private bool TryGetActor(out PayrollActor actor)
    {
        if (!Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId) ||
            !Guid.TryParse(User.FindFirstValue(AttendanceAuthenticationDefaults.CompanyIdClaim), out var companyId) ||
            !Guid.TryParse(User.FindFirstValue(AttendanceAuthenticationDefaults.MembershipIdClaim), out var membershipId))
        {
            actor = null!;
            return false;
        }

        actor = new PayrollActor(userId, companyId, membershipId);
        return true;
    }

    private static bool TryParseWorkDays(IEnumerable<string> values, out DayOfWeek[] workDays)
    {
        var parsed = new List<DayOfWeek>();
        foreach (var value in values)
        {
            if (!Enum.TryParse(value, ignoreCase: true, out DayOfWeek day) || !Enum.IsDefined(day))
            {
                workDays = [];
                return false;
            }

            parsed.Add(day);
        }

        workDays = parsed.Distinct().ToArray();
        return workDays.Length > 0;
    }

    private static CompanyPayrollSettingsResponse ToResponse(CompanyPayrollConfiguration settings) => new(
        settings.IsConfigured,
        settings.PayrollPeriodStartDay,
        settings.TimeZoneId,
        settings.DefaultWorkStartTime?.ToString("HH:mm"),
        settings.DefaultWorkEndTime?.ToString("HH:mm"),
        settings.DefaultWorkDays.Select(day => day.ToString()).ToArray(),
        settings.UpdatedAtUtc);
}

public sealed class SaveCompanyPayrollSettingsRequest
{
    [Range(1, 31)]
    public int PayrollPeriodStartDay { get; init; }

    [Required]
    [StringLength(100, MinimumLength = 1)]
    public string TimeZoneId { get; init; } = string.Empty;

    [Required]
    public TimeOnly? DefaultWorkStartTime { get; init; }

    [Required]
    public TimeOnly? DefaultWorkEndTime { get; init; }

    [Required]
    [MinLength(1)]
    public string[] DefaultWorkDays { get; init; } = [];
}

public sealed record CompanyPayrollSettingsResponse(
    bool IsConfigured,
    int? PayrollPeriodStartDay,
    string TimeZoneId,
    string? DefaultWorkStartTime,
    string? DefaultWorkEndTime,
    IReadOnlyList<string> DefaultWorkDays,
    DateTimeOffset? UpdatedAtUtc);

public sealed record CompanyPayrollSettingsErrorResponse(string Error);
