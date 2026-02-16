using MediatR;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SAED_PortalEmpleado.Api.Services;
using SAED_PortalEmpleado.Application.Auth.Commands.Login;
using SAED_PortalEmpleado.Application.Auth.Queries.GetCurrentUser;

namespace SAED_PortalEmpleado.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly ILogger<AuthController> _logger;
    private readonly IAntiforgery _antiforgery;
    private readonly IConfiguration _configuration;
    private readonly IGoogleDirectoryCuilService _directoryCuilService;

    public AuthController(
        IMediator mediator,
        ILogger<AuthController> logger,
        IAntiforgery antiforgery,
        IConfiguration configuration,
        IGoogleDirectoryCuilService directoryCuilService)
    {
        _mediator = mediator;
        _logger = logger;
        _antiforgery = antiforgery;
        _configuration = configuration;
        _directoryCuilService = directoryCuilService;
    }

    /// <summary>
    /// Gets CSRF token for protected operations
    /// </summary>
    [HttpGet("csrf-token")]
    [AllowAnonymous]
    public IActionResult GetCsrfToken()
    {
        var tokens = _antiforgery.GetAndStoreTokens(HttpContext);
        return Ok(new { token = tokens.RequestToken });
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

        if (authenticateResult.Principal == null)
        {
            _logger.LogWarning("No principal found in authentication result");
            return Unauthorized("No principal found");
        }

        var email = authenticateResult.Principal.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value;
        if (!IsEmailAllowed(email))
        {
            _logger.LogWarning("Login blocked for email: {Email}", email ?? "(null)");
            return Forbid();
        }

        // Use MediatR to handle the authentication logic
        var cuilFromDirectory = await _directoryCuilService.GetCuilByEmailAsync(email);
        var command = new HandleGoogleCallbackCommand(authenticateResult.Principal, cuilFromDirectory);
        var result = await _mediator.Send(command);

        if (!result.Success || result.Principal == null)
        {
            _logger.LogWarning("Failed to process Google authentication: {Error}", result.ErrorMessage);
            return BadRequest(result.ErrorMessage ?? "Authentication processing failed");
        }

        // Sign in with Cookie scheme to establish the session
        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, result.Principal);

        // Redirect to provided return URL after successful authentication
        string? returnUrl = null;
        if (authenticateResult.Properties?.Items != null &&
            authenticateResult.Properties.Items.TryGetValue("returnUrl", out var returnUrlValue))
        {
            returnUrl = returnUrlValue;
        }
        if (!string.IsNullOrWhiteSpace(returnUrl) &&
            (returnUrl.StartsWith("/") || returnUrl.StartsWith("http://localhost:5173", StringComparison.OrdinalIgnoreCase)))
        {
            return Redirect(returnUrl);
        }

        return Redirect("/");
    }

    private bool IsEmailAllowed(string? email)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            return false;
        }

        var allowedDomains = _configuration
            .GetSection("Authentication:AllowedEmailDomains")
            .Get<string[]>();

        if (allowedDomains == null || allowedDomains.Length == 0)
        {
            return true;
        }

        var atIndex = email.LastIndexOf('@');
        if (atIndex <= 0 || atIndex == email.Length - 1)
        {
            return false;
        }

        var domain = email[(atIndex + 1)..];
        return allowedDomains.Any(d =>
            string.Equals(d, domain, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Logs out the current user
    /// </summary>
    [HttpPost("logout")]
    [Authorize]
    [ValidateAntiForgeryToken]
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
    [ResponseCache(Duration = 300, VaryByHeader = "Cookie")] // Cache for 5 minutes per user
    public async Task<IActionResult> GetCurrentUser()
    {
        try
        {
            var query = new GetCurrentUserQuery();
            var result = await _mediator.Send(query);
            return Ok(result);
        }
        catch (UnauthorizedAccessException)
        {
            return Unauthorized();
        }
        catch (KeyNotFoundException)
        {
            return NotFound("Employee not found");
        }
    }
}
