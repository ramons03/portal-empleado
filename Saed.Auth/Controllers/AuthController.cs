using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Saed.Auth.Models;
using Saed.Auth.Services;
using System.Security.Claims;

namespace Saed.Auth.Controllers;

public class AuthController : Controller
{
    private readonly IDomainValidationService _domainValidationService;
    private readonly IJwtTokenService _jwtTokenService;
    private readonly ILogger<AuthController> _logger;

    public AuthController(
        IDomainValidationService domainValidationService,
        IJwtTokenService jwtTokenService,
        ILogger<AuthController> logger)
    {
        _domainValidationService = domainValidationService;
        _jwtTokenService = jwtTokenService;
        _logger = logger;
    }

    [HttpGet]
    public IActionResult Login(string? policy = null)
    {
        ViewBag.Policy = policy;
        return View();
    }

    [HttpGet]
    public IActionResult GoogleLogin(string policy)
    {
        if (!Enum.TryParse<DomainPolicyType>(policy, true, out var policyType))
        {
            return BadRequest("Invalid policy type");
        }

        var properties = new AuthenticationProperties
        {
            RedirectUri = Url.Action(nameof(GoogleResponse), new { policy }),
            Items = { ["policy"] = policy }
        };

        return Challenge(properties, GoogleDefaults.AuthenticationScheme);
    }

    [HttpGet]
    public async Task<IActionResult> GoogleResponse(string policy)
    {
        if (!Enum.TryParse<DomainPolicyType>(policy, true, out var policyType))
        {
            return BadRequest("Invalid policy type");
        }

        var authenticateResult = await HttpContext.AuthenticateAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        
        if (!authenticateResult.Succeeded)
        {
            _logger.LogWarning("Google authentication failed");
            return RedirectToAction(nameof(Login), new { error = "Authentication failed" });
        }

        var email = authenticateResult.Principal?.FindFirst(ClaimTypes.Email)?.Value;
        var name = authenticateResult.Principal?.FindFirst(ClaimTypes.Name)?.Value ?? "Unknown";

        if (string.IsNullOrEmpty(email))
        {
            _logger.LogWarning("Email claim not found");
            return RedirectToAction(nameof(Login), new { error = "Email not found" });
        }

        // Validate email domain based on policy
        if (!_domainValidationService.ValidateEmailDomain(email, policyType))
        {
            _logger.LogWarning("Email {Email} does not match policy {Policy}", email, policy);
            return RedirectToAction(nameof(Login), new { error = $"Email domain not allowed for {policy} policy" });
        }

        // Generate internal JWT token
        var jwtToken = _jwtTokenService.GenerateToken(email, name, policyType);

        // Return token to view
        ViewBag.Token = jwtToken;
        ViewBag.Email = email;
        ViewBag.Name = name;
        ViewBag.Policy = policy;

        return View("Token");
    }

    [HttpGet]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return RedirectToAction(nameof(Login));
    }
}
