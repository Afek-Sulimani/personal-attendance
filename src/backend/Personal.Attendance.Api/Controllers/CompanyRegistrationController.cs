using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Personal.Attendance.Application.Companies;

namespace Personal.Attendance.Api.Controllers;

[ApiController]
[Route("api/companies")]
public sealed class CompanyRegistrationController(
    ICompanyRegistrationService companyRegistrationService) : ControllerBase
{
    [HttpPost("registration")]
    [AllowAnonymous]
    [ValidateAntiForgeryToken]
    [ProducesResponseType<CompanyRegistrationResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<CompanyRegistrationResponse>> Register(
        CompanyRegistrationRequest request,
        CancellationToken cancellationToken)
    {
        var result = await companyRegistrationService.RegisterAsync(
            new CompanyRegistrationCommand(
                request.CompanyName,
                request.CompanyRepresentativeName,
                request.CompanyRepresentativeEmail),
            cancellationToken);

        return result.Status switch
        {
            CompanyRegistrationOperationStatus.Succeeded when result.Registration is not null =>
                StatusCode(StatusCodes.Status201Created, new CompanyRegistrationResponse(
                    result.Registration.CompanyName,
                    result.Registration.CompanyCode,
                    result.Registration.CompanyRepresentativeName,
                    result.Registration.CompanyRepresentativeEmail,
                    result.Registration.ActivationUrl)),
            CompanyRegistrationOperationStatus.Invalid =>
                BadRequest(new ErrorResponse("Company and company representative details are required.")),
            CompanyRegistrationOperationStatus.Conflict =>
                Conflict(new ErrorResponse("The company registration could not be created. Please try again.")),
            _ => StatusCode(
                StatusCodes.Status503ServiceUnavailable,
                new ErrorResponse("Company registration is temporarily unavailable."))
        };
    }
}

public sealed class CompanyRegistrationRequest
{
    [Required]
    [StringLength(200, MinimumLength = 1)]
    public string CompanyName { get; init; } = string.Empty;

    [Required]
    [StringLength(200, MinimumLength = 1)]
    public string CompanyRepresentativeName { get; init; } = string.Empty;

    [Required]
    [EmailAddress]
    [StringLength(256)]
    public string CompanyRepresentativeEmail { get; init; } = string.Empty;
}

public sealed record CompanyRegistrationResponse(
    string CompanyName,
    string CompanyCode,
    string CompanyRepresentativeName,
    string CompanyRepresentativeEmail,
    string ActivationUrl);
