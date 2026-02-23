using System.Security.Claims;

namespace SAED_PortalEmpleado.Application.Auth.Commands.Login;

public record HandleGoogleCallbackResponse(
    bool Success,
    ClaimsPrincipal? Principal,
    string? ErrorMessage = null
);
