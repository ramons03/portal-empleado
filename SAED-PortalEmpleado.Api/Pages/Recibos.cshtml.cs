using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Security.Claims;

namespace SAED_PortalEmpleado.Api.Pages;

[Authorize]
public class RecibosModel : PageModel
{
    public string EmployeeName { get; set; } = string.Empty;

    public void OnGet()
    {
        EmployeeName = User.FindFirst(ClaimTypes.Name)?.Value ?? "Usuario";
    }
}
