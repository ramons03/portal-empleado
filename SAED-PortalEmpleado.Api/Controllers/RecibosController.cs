using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SAED_PortalEmpleado.Application.Common.Interfaces;
using SAED_PortalEmpleado.Domain.Entities;
using SAED_PortalEmpleado.Infrastructure.Persistence;

namespace SAED_PortalEmpleado.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class RecibosController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly IConfiguration _configuration;

    public RecibosController(
        ApplicationDbContext context,
        ICurrentUserService currentUserService,
        IDateTimeProvider dateTimeProvider,
        IConfiguration configuration)
    {
        _context = context;
        _currentUserService = currentUserService;
        _dateTimeProvider = dateTimeProvider;
        _configuration = configuration;
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
        var items = Array.Empty<ReciboItem>();
        if (ShouldUseMockRecibos())
        {
            items = BuildMockRecibos();
        }
        var response = new RecibosResponse(employee.Cuil, items);
        return Ok(response);
    }

    /// <summary>
    /// Registers a view/open event for receipts (for analytics).
    /// </summary>
    [HttpPost("view")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> LogView([FromBody] RecibosViewRequest request)
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

        var action = string.IsNullOrWhiteSpace(request.Action) ? "page" : request.Action;
        if (action != "page" && action != "open")
        {
            return BadRequest(new { message = "Acción inválida." });
        }

        var viewEvent = new ReciboViewEvent
        {
            Id = Guid.NewGuid(),
            GoogleSub = googleSub,
            Cuil = employee.Cuil,
            Action = action,
            ReciboId = request.ReciboId,
            ViewedAtUtc = _dateTimeProvider.UtcNow
        };

        _context.ReciboViewEvents.Add(viewEvent);
        await _context.SaveChangesAsync();

        return Ok(new { ok = true });
    }

    /// <summary>
    /// Basic stats for recibos views.
    /// </summary>
    [HttpGet("stats")]
    public async Task<IActionResult> GetStats([FromQuery] int days = 30)
    {
        if (days <= 0 || days > 365) days = 30;
        var from = _dateTimeProvider.UtcNow.Date.AddDays(-days + 1);

        var total = await _context.ReciboViewEvents.CountAsync(e => e.ViewedAtUtc >= from);
        var opens = await _context.ReciboViewEvents.CountAsync(e => e.ViewedAtUtc >= from && e.Action == "open");
        var pages = await _context.ReciboViewEvents.CountAsync(e => e.ViewedAtUtc >= from && e.Action == "page");

        return Ok(new { from, days, total, pages, opens });
    }

    private bool ShouldUseMockRecibos()
    {
        return _configuration.GetValue<bool>("Recibos:MockData");
    }

    private ReciboItem[] BuildMockRecibos()
    {
        var now = _dateTimeProvider.UtcNow;
        return new[]
        {
            new ReciboItem("R-2026-01", "Enero 2026", 182_450.75m, "ARS", "Emitido", now.AddDays(-45)),
            new ReciboItem("R-2025-12", "Diciembre 2025", 176_900.10m, "ARS", "Emitido", now.AddDays(-75)),
            new ReciboItem("R-2025-11", "Noviembre 2025", 171_320.40m, "ARS", "Emitido", now.AddDays(-105))
        };
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

public record RecibosViewRequest(string? Action, string? ReciboId);
