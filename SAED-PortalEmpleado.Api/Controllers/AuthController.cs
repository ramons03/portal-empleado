using MediatR;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
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

    public AuthController(IMediator mediator, ILogger<AuthController> logger, IAntiforgery antiforgery)
    {
        _mediator = mediator;
        _logger = logger;
        _antiforgery = antiforgery;
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

        // Use MediatR to handle the authentication logic
        var command = new HandleGoogleCallbackCommand(authenticateResult.Principal);
        var result = await _mediator.Send(command);

        if (!result.Success || result.Principal == null)
        {
            _logger.LogWarning("Failed to process Google authentication: {Error}", result.ErrorMessage);
            return BadRequest(result.ErrorMessage ?? "Authentication processing failed");
        }

        // Sign in with Cookie scheme to establish the session
        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, result.Principal);

        // Redirect to home page after successful authentication
        return Redirect("/");
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
