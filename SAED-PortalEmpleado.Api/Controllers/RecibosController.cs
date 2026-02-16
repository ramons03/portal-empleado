using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SAED_PortalEmpleado.Application.Common.Interfaces;
using SAED_PortalEmpleado.Infrastructure.Persistence;

namespace SAED_PortalEmpleado.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class RecibosController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;

    public RecibosController(ApplicationDbContext context, ICurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
    }

    /// <summary>
    /// Returns payroll receipts for the current user by CUIL.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetRecibos()
    {
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

        if (string.IsNullOrWhiteSpace(employee.Cuil))
        {
            return BadRequest(new { message = "CUIL no configurado para el usuario." });
        }

        // TODO: Integrate with payroll system using employee.Cuil
        var response = new RecibosResponse(employee.Cuil, Array.Empty<ReciboItem>());
        return Ok(response);
    }
}

public record RecibosResponse(string Cuil, IReadOnlyList<ReciboItem> Items);

public record ReciboItem(
    string Id,
    string Periodo,
    decimal Importe,
    string Moneda,
    string Estado,
    DateTime FechaEmision
);
