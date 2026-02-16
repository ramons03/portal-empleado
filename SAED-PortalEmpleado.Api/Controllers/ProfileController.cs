using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SAED_PortalEmpleado.Application.Common.Interfaces;
using SAED_PortalEmpleado.Infrastructure.Persistence;
using System.Text.RegularExpressions;

namespace SAED_PortalEmpleado.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ProfileController : ControllerBase
{
    private static readonly Regex CuilRegex = new(@"^\d{11}$", RegexOptions.Compiled);

    private readonly ApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;

    public ProfileController(ApplicationDbContext context, ICurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
    }

    [HttpPost("cuil")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateCuil([FromBody] UpdateCuilRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Cuil) || !CuilRegex.IsMatch(request.Cuil))
        {
            return BadRequest(new { message = "CUIL inválido. Debe tener 11 dígitos." });
        }

        var googleSub = _currentUserService.GoogleSub;
        if (string.IsNullOrWhiteSpace(googleSub))
        {
            return Unauthorized();
        }

        var employee = await _context.Employees.SingleOrDefaultAsync(e => e.GoogleSub == googleSub);
        if (employee == null)
        {
            return NotFound("Employee not found");
        }

        employee.Cuil = request.Cuil;
        await _context.SaveChangesAsync();

        return Ok(new { cuil = employee.Cuil });
    }
}

public record UpdateCuilRequest(string Cuil);
