using MediatR;
using System.Security.Claims;

namespace SAED_PortalEmpleado.Application.Auth.Commands.Login;

public record HandleGoogleCallbackCommand(ClaimsPrincipal Principal) : IRequest<HandleGoogleCallbackResponse>;
