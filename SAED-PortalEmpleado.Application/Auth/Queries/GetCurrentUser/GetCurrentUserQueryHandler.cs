using MediatR;
using SAED_PortalEmpleado.Application.Common.Interfaces;

namespace SAED_PortalEmpleado.Application.Auth.Queries.GetCurrentUser;

public class GetCurrentUserQueryHandler : IRequestHandler<GetCurrentUserQuery, GetCurrentUserResponse>
{
    private readonly IEmployeeRepository _employeeRepository;
    private readonly ICurrentUserService _currentUserService;

    public GetCurrentUserQueryHandler(IEmployeeRepository employeeRepository, ICurrentUserService currentUserService)
    {
        _employeeRepository = employeeRepository;
        _currentUserService = currentUserService;
    }

    public async Task<GetCurrentUserResponse> Handle(GetCurrentUserQuery request, CancellationToken cancellationToken)
    {
        var googleSub = _currentUserService.GoogleSub ?? throw new UnauthorizedAccessException("User is not authenticated");

        var employee = await _employeeRepository.GetByGoogleSubAsync(googleSub, cancellationToken)
            ?? throw new KeyNotFoundException("Employee not found");

        return new GetCurrentUserResponse(
            employee.Id,
            employee.Email,
            employee.FullName,
            employee.Cuil,
            employee.PictureUrl,
            employee.CreatedAt
        );
    }
}
