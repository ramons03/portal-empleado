using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SAED_PortalEmpleado.Domain.Entities;
using SAED_PortalEmpleado.Infrastructure.Persistence;

namespace SAED_PortalEmpleado.Api.Controllers;

[ApiController]
[Route("api/company-profile")]
[Authorize]
public class CompanyProfileController : ControllerBase
{
    private static readonly Regex CuitRegex = new(@"^\d{2}-?\d{8}-?\d$", RegexOptions.Compiled);

    private readonly ApplicationDbContext _context;

    public CompanyProfileController(ApplicationDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<IActionResult> Get()
    {
        var company = await _context.CompanyProfiles
            .AsNoTracking()
            .OrderByDescending(c => c.UpdatedAtUtc ?? c.CreatedAtUtc)
            .FirstOrDefaultAsync();

        if (company is null)
        {
            return NotFound(new { message = "No hay datos de empresa configurados." });
        }

        return Ok(ToResponse(company));
    }

    [HttpPut]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Upsert([FromBody] UpsertCompanyProfileRequest request)
    {
        var displayName = request.DisplayName?.Trim();
        var cuit = request.Cuit?.Trim();

        if (string.IsNullOrWhiteSpace(displayName))
        {
            return BadRequest(new { message = "DisplayName es obligatorio." });
        }

        if (string.IsNullOrWhiteSpace(cuit) || !CuitRegex.IsMatch(cuit))
        {
            return BadRequest(new { message = "CUIT inválido. Formato esperado: 30-12345678-9." });
        }

        var company = await _context.CompanyProfiles
            .OrderByDescending(c => c.UpdatedAtUtc ?? c.CreatedAtUtc)
            .FirstOrDefaultAsync();

        var now = DateTime.UtcNow;
        if (company is null)
        {
            company = new CompanyProfile
            {
                Id = Guid.NewGuid(),
                CreatedAtUtc = now
            };
            _context.CompanyProfiles.Add(company);
        }
        else
        {
            company.UpdatedAtUtc = now;
        }

        company.DisplayName = displayName;
        company.Cuit = cuit;
        company.AddressLine = NormalizeOptional(request.AddressLine);
        company.City = NormalizeOptional(request.City);
        company.Province = NormalizeOptional(request.Province);
        company.PostalCode = NormalizeOptional(request.PostalCode);
        company.Country = NormalizeOptional(request.Country);

        await _context.SaveChangesAsync();
        return Ok(ToResponse(company));
    }

    private static string? NormalizeOptional(string? value)
    {
        var trimmed = value?.Trim();
        return string.IsNullOrWhiteSpace(trimmed) ? null : trimmed;
    }

    private static CompanyProfileResponse ToResponse(CompanyProfile company)
    {
        return new CompanyProfileResponse(
            company.Id,
            company.DisplayName,
            company.Cuit,
            company.AddressLine,
            company.City,
            company.Province,
            company.PostalCode,
            company.Country,
            company.CreatedAtUtc,
            company.UpdatedAtUtc);
    }
}

public record UpsertCompanyProfileRequest(
    string DisplayName,
    string Cuit,
    string? AddressLine,
    string? City,
    string? Province,
    string? PostalCode,
    string? Country
);

public record CompanyProfileResponse(
    Guid Id,
    string DisplayName,
    string Cuit,
    string? AddressLine,
    string? City,
    string? Province,
    string? PostalCode,
    string? Country,
    DateTime CreatedAtUtc,
    DateTime? UpdatedAtUtc
);
