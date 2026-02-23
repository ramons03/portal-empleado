namespace SAED_PortalEmpleado.Application.Auth.Queries.GetCurrentUser;

public record GetCurrentUserResponse(
    Guid Id,
    string Email,
    string FullName,
    string? Cuil,
    string? PictureUrl,
    DateTime CreatedAt
);
