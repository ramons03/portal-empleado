using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SAED_PortalEmpleado.Domain.Entities;
using SAED_PortalEmpleado.Infrastructure.Persistence;
using System.Security.Claims;

namespace SAED_PortalEmpleado.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<AuthController> _logger;

    public AuthController(ApplicationDbContext context, ILogger<AuthController> logger)
    {
        _context = context;
        _logger = logger;
    }

    /// <summary>
    /// Initiates Google OAuth login flow
    /// </summary>
    [HttpGet("login")]
    [AllowAnonymous]
    public IActionResult Login(string returnUrl = "/")
    {
        var properties = new AuthenticationProperties
        {
            RedirectUri = Url.Action(nameof(GoogleCallback)),
            Items = { { "returnUrl", returnUrl } }
        };
        return Challenge(properties, GoogleDefaults.AuthenticationScheme);
    }

    /// <summary>
    /// Google OAuth callback endpoint
    /// </summary>
    [HttpGet("google-callback")]
    [AllowAnonymous]
    public async Task<IActionResult> GoogleCallback()
    {
        // Authenticate with Google scheme to get the external authentication result
        var authenticateResult = await HttpContext.AuthenticateAsync(GoogleDefaults.AuthenticationScheme);
        
        if (!authenticateResult.Succeeded)
        {
            _logger.LogWarning("Authentication failed");
            return Unauthorized("Authentication failed");
        }

        var claims = authenticateResult.Principal?.Claims;
        if (claims == null)
        {
            _logger.LogWarning("No claims found in authentication result");
            return Unauthorized("No claims found");
        }

        // Extract claims from Google
        var googleSub = claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier)?.Value;
        var email = claims.FirstOrDefault(c => c.Type == ClaimTypes.Email)?.Value;
        var name = claims.FirstOrDefault(c => c.Type == ClaimTypes.Name)?.Value;
        var picture = claims.FirstOrDefault(c => c.Type == "picture" || c.Type == "urn:google:picture")?.Value;

        if (string.IsNullOrEmpty(googleSub) || string.IsNullOrEmpty(email))
        {
            _logger.LogWarning("Required claims (sub or email) are missing");
            return BadRequest("Required claims are missing");
        }

        // Persist or update employee in database
        var employee = await _context.Employees
            .FirstOrDefaultAsync(e => e.GoogleSub == googleSub);

        if (employee == null)
        {
            // Create new employee
            employee = new Employee
            {
                Id = Guid.NewGuid(),
                GoogleSub = googleSub,
                Email = email,
                FullName = name ?? email,
                PictureUrl = picture,
                CreatedAt = DateTime.UtcNow
            };
            _context.Employees.Add(employee);
            _logger.LogInformation("Creating new employee: {Email}", email);
        }
        else
        {
            // Update existing employee
            employee.Email = email;
            employee.FullName = name ?? email;
            employee.PictureUrl = picture;
            _logger.LogInformation("Updating existing employee: {Email}", email);
        }

        await _context.SaveChangesAsync();

        // Sign in with Cookie scheme to establish the session
        // Only include necessary claims to minimize cookie payload
        var cookieClaims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, googleSub),
            new Claim(ClaimTypes.Email, email),
            new Claim(ClaimTypes.Name, name ?? email)
        };
        
        if (!string.IsNullOrEmpty(picture))
        {
            cookieClaims.Add(new Claim("picture", picture));
        }

        var identity = new ClaimsIdentity(cookieClaims, CookieAuthenticationDefaults.AuthenticationScheme);
        var principal = new ClaimsPrincipal(identity);
        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal);

        return Ok(new
        {
            message = "Authentication successful",
            employee = new
            {
                employee.Id,
                employee.Email,
                employee.FullName,
                employee.PictureUrl
            }
        });
    }

    /// <summary>
    /// Logs out the current user
    /// </summary>
    [HttpPost("logout")]
    [Authorize]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return Ok(new { message = "Logged out successfully" });
    }

    /// <summary>
    /// Gets the current authenticated user info
    /// </summary>
    [HttpGet("me")]
    [Authorize]
    public async Task<IActionResult> GetCurrentUser()
    {
        var googleSub = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        
        if (string.IsNullOrEmpty(googleSub))
        {
            return Unauthorized();
        }

        var employee = await _context.Employees
            .FirstOrDefaultAsync(e => e.GoogleSub == googleSub);

        if (employee == null)
        {
            return NotFound("Employee not found");
        }

        return Ok(new
        {
            employee.Id,
            employee.Email,
            employee.FullName,
            employee.PictureUrl,
            employee.CreatedAt
        });
    }
}
