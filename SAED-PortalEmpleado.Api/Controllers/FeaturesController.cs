using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace SAED_PortalEmpleado.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class FeaturesController : ControllerBase
{
    private readonly IConfiguration _configuration;

    public FeaturesController(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    [HttpGet]
    [AllowAnonymous]
    [ResponseCache(Duration = 60)]
    public IActionResult GetFeatures()
    {
        var vacaciones = _configuration.GetValue<bool>("Features:Vacaciones");
        return Ok(new { vacaciones });
    }
}
