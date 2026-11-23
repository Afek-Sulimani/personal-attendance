using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Personal.Attendance.Api.Authentication;
using Personal.Attendance.Application.Payroll;

namespace Personal.Attendance.Api.Controllers;

[ApiController]
[Route("api/payroll/employees")]
[Authorize(
    AuthenticationSchemes = AttendanceAuthenticationDefaults.SchemeName,
    Roles = "Admin,PayrollRepresentative")]
public sealed class PayrollEmployeesController(
    IPayrollWorkScheduleService workScheduleService,
    ILogger<PayrollEmployeesController> logger) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<PayrollEmployeeResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<IReadOnlyList<PayrollEmployeeResponse>>> GetEmployees(
        CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actor)) return Unauthorized();

        try
        {
            var employees = await workScheduleService.GetEmployeesAsync(actor, cancellationToken);
            return Ok(employees.Select(employee => new PayrollEmployeeResponse(
                employee.MembershipId,
                employee.DisplayName,
                employee.EmployeeNumber)).ToArray());
        }
        catch (PayrollAccessDeniedException exception)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new PayrollErrorResponse(exception.Message));
        }
        catch (PayrollUnavailableException)
        {
            logger.LogWarning("The payroll employee list could not be loaded.");
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new PayrollErrorResponse("Payroll data is temporarily unavailable."));
        }
    }

    [HttpGet("{employeeMembershipId:guid}/work-days")]
    [ProducesResponseType<MonthlyWorkScheduleResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<PayrollErrorResponse>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<PayrollErrorResponse>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<PayrollErrorResponse>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<PayrollErrorResponse>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<PayrollErrorResponse>(StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<MonthlyWorkScheduleResponse>> GetWorkDays(
        Guid employeeMembershipId,
        [FromQuery, Range(2000, 9999)] int year,
        [FromQuery, Range(1, 12)] int month,
        CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actor)) return Unauthorized();

        try
        {
            var schedule = await workScheduleService.GetScheduleAsync(
                actor, employeeMembershipId, year, month, cancellationToken);
            return Ok(ToResponse(schedule));
        }
        catch (PayrollAccessDeniedException exception)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new PayrollErrorResponse(exception.Message));
        }
        catch (PayrollEmployeeNotFoundException exception)
        {
            return NotFound(new PayrollErrorResponse(exception.Message));
        }
        catch (ArgumentOutOfRangeException exception)
        {
            return BadRequest(new PayrollErrorResponse(exception.Message));
        }
        catch (PayrollUnavailableException)
        {
            logger.LogWarning("A monthly work schedule could not be loaded.");
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new PayrollErrorResponse("Payroll data is temporarily unavailable."));
        }
    }

    [HttpPut("{employeeMembershipId:guid}/work-days")]
    [ValidateAntiForgeryToken]
    [ProducesResponseType<MonthlyWorkScheduleResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<PayrollErrorResponse>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<PayrollErrorResponse>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<PayrollErrorResponse>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<PayrollErrorResponse>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<PayrollErrorResponse>(StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<MonthlyWorkScheduleResponse>> SaveWorkDays(
        Guid employeeMembershipId,
        [FromQuery, Range(2000, 9999)] int year,
        [FromQuery, Range(1, 12)] int month,
        SaveMonthlyWorkScheduleRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actor)) return Unauthorized();

        try
        {
            var schedule = await workScheduleService.SaveScheduleAsync(
                actor,
                employeeMembershipId,
                year,
                month,
                request.WorkingDates,
                cancellationToken);
            return Ok(ToResponse(schedule));
        }
        catch (PayrollAccessDeniedException exception)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new PayrollErrorResponse(exception.Message));
        }
        catch (PayrollEmployeeNotFoundException exception)
        {
            return NotFound(new PayrollErrorResponse(exception.Message));
        }
        catch (ArgumentException exception)
        {
            return BadRequest(new PayrollErrorResponse(exception.Message));
        }
        catch (PayrollUnavailableException)
        {
            logger.LogWarning("A monthly work schedule could not be saved.");
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new PayrollErrorResponse("Payroll data is temporarily unavailable."));
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

    private static MonthlyWorkScheduleResponse ToResponse(MonthlyWorkSchedule schedule) => new(
        schedule.EmployeeMembershipId,
        schedule.Year,
        schedule.Month,
        schedule.IsConfigured,
        schedule.WorkingDates);
}

public sealed record PayrollEmployeeResponse(Guid MembershipId, string DisplayName, string? EmployeeNumber);

public sealed record MonthlyWorkScheduleResponse(
    Guid EmployeeMembershipId,
    int Year,
    int Month,
    bool IsConfigured,
    IReadOnlyList<DateOnly> WorkingDates);

public sealed class SaveMonthlyWorkScheduleRequest
{
    [Required]
    public DateOnly[] WorkingDates { get; init; } = [];
}

public sealed record PayrollErrorResponse(string Error);
