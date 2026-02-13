using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Security.Claims;

namespace SAED_PortalEmpleado.Api.Pages;

public class IndexModel : PageModel
{
    public string EmployeeName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string PictureUrl { get; set; } = string.Empty;

    public void OnGet()
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            EmployeeName = User.FindFirst(ClaimTypes.Name)?.Value ?? "Usuario";
            Email = User.FindFirst(ClaimTypes.Email)?.Value ?? string.Empty;
            PictureUrl = User.FindFirst("picture")?.Value ?? string.Empty;
        }
    }
}
