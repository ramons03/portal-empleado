using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Configuration;
using System.Security.Claims;

namespace SAED_PortalEmpleado.Api.Pages;

[Authorize]
public class VacacionesModel : PageModel
{
    public string EmployeeName { get; set; } = string.Empty;
    private readonly IConfiguration _configuration;

    public VacacionesModel(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public IActionResult OnGet()
    {
        if (!_configuration.GetValue<bool>("Features:Vacaciones"))
        {
            return Redirect("/");
        }
        EmployeeName = User.FindFirst(ClaimTypes.Name)?.Value ?? "Usuario";
        return Page();
    }
}
