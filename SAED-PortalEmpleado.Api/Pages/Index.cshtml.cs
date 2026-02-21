using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using System.Security.Claims;

namespace SAED_PortalEmpleado.Api.Pages;

public class IndexModel : PageModel
{
    public string EmployeeName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string PictureUrl { get; set; } = string.Empty;
    public bool ShowVacaciones { get; private set; }
    public bool DevLoginEnabled { get; private set; }
    public string LoginUrl { get; private set; } = "/api/auth/login?returnUrl=/";
    public string LoginLabel { get; private set; } = "Iniciar Sesión con Google";

    private readonly IConfiguration _configuration;
    private readonly IHostEnvironment _environment;

    public IndexModel(IConfiguration configuration, IHostEnvironment environment)
    {
        _configuration = configuration;
        _environment = environment;
    }

    public void OnGet()
    {
        ShowVacaciones = _configuration.GetValue<bool>("Features:Vacaciones");
        DevLoginEnabled = _environment.IsDevelopment()
            && _configuration.GetValue<bool>("Authentication:DevLogin:Enabled");

        if (DevLoginEnabled)
        {
            LoginUrl = "/api/auth/dev-login?returnUrl=/";
            LoginLabel = "Login de desarrollo";
        }

        if (User.Identity?.IsAuthenticated == true)
        {
            EmployeeName = User.FindFirst(ClaimTypes.Name)?.Value ?? "Usuario";
            Email = User.FindFirst(ClaimTypes.Email)?.Value ?? string.Empty;
            PictureUrl = User.FindFirst("picture")?.Value ?? string.Empty;
        }
    }
}
