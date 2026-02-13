using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SAED_PortalEmpleado.Domain.Entities;
using SAED_PortalEmpleado.Infrastructure.Persistence;

namespace SAED_PortalEmpleado.Api.Controllers;

/// <summary>
/// Employees API Controller
/// Note: For simplicity, this controller directly uses DbContext.
/// In a production application, consider implementing Repository pattern
/// or CQRS with MediatR to maintain better separation of concerns.
/// </summary>
[Authorize]
[ApiController]
[Route("api/[controller]")]
public class EmployeesController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<EmployeesController> _logger;

    public EmployeesController(ApplicationDbContext context, ILogger<EmployeesController> logger)
    {
        _context = context;
        _logger = logger;
    }

    /// <summary>
    /// Get all employees
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<IEnumerable<Employee>>> GetEmployees()
    {
        _logger.LogInformation("Getting all employees");
        return await _context.Employees.ToListAsync();
    }

    /// <summary>
    /// Get employee by Id
    /// </summary>
    [HttpGet("{id}")]
    public async Task<ActionResult<Employee>> GetEmployee(Guid id)
    {
        _logger.LogInformation("Getting employee with Id: {EmployeeId}", id);
        var employee = await _context.Employees.FindAsync(id);

        if (employee == null)
        {
            return NotFound();
        }

        return employee;
    }

    /// <summary>
    /// Create a new employee
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<Employee>> CreateEmployee(Employee employee)
    {
        _logger.LogInformation("Creating new employee: {Email}", employee.Email);
        
        employee.Id = Guid.NewGuid();
        employee.CreatedAt = DateTime.UtcNow;

        _context.Employees.Add(employee);
        await _context.SaveChangesAsync();

        return CreatedAtAction(nameof(GetEmployee), new { id = employee.Id }, employee);
    }

    /// <summary>
    /// Update an existing employee
    /// </summary>
    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateEmployee(Guid id, Employee employee)
    {
        if (id != employee.Id)
        {
            return BadRequest();
        }

        var existingEmployee = await _context.Employees.FindAsync(id);
        if (existingEmployee == null)
        {
            return NotFound();
        }

        // Update properties but preserve CreatedAt
        existingEmployee.GoogleSub = employee.GoogleSub;
        existingEmployee.Email = employee.Email;
        existingEmployee.FullName = employee.FullName;
        existingEmployee.PictureUrl = employee.PictureUrl;

        try
        {
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            throw;
        }

        return NoContent();
    }

    /// <summary>
    /// Delete an employee
    /// </summary>
    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteEmployee(Guid id)
    {
        _logger.LogInformation("Deleting employee with Id: {EmployeeId}", id);
        var employee = await _context.Employees.FindAsync(id);
        
        if (employee == null)
        {
            return NotFound();
        }

        _context.Employees.Remove(employee);
        await _context.SaveChangesAsync();

        return NoContent();
    }

    private async Task<bool> EmployeeExists(Guid id)
    {
        return await _context.Employees.AnyAsync(e => e.Id == id);
    }
}
