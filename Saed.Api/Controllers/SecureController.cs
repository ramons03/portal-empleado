using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace Saed.Api.Controllers;

[ApiController]
[Route("api")]
public class SecureController : ControllerBase
{
    private readonly ILogger<SecureController> _logger;

    public SecureController(ILogger<SecureController> logger)
    {
        _logger = logger;
    }

    [HttpGet("secure")]
    [Authorize]
    public IActionResult GetSecureData()
    {
        var email = User.FindFirst(ClaimTypes.Email)?.Value;
        var name = User.FindFirst(ClaimTypes.Name)?.Value;
        var policy = User.FindFirst("policy")?.Value;
        
        _logger.LogInformation("Secure endpoint accessed by {Email} with policy {Policy}", email, policy);

        return Ok(new
        {
            message = "This is secure data from the API",
            timestamp = DateTime.UtcNow,
            user = new
            {
                email,
                name,
                policy
            }
        });
    }

    [HttpGet("public")]
    public IActionResult GetPublicData()
    {
        return Ok(new
        {
            message = "This is public data from the API",
            timestamp = DateTime.UtcNow
        });
    }
}
