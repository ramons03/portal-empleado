using MediatR;

namespace SAED_PortalEmpleado.Application.Auth.Queries.GetCurrentUser;

public record GetCurrentUserQuery : IRequest<GetCurrentUserResponse>;
