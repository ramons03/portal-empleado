using MediatR;
using Microsoft.Extensions.Logging;
using SAED_PortalEmpleado.Application.Common.Interfaces;
using SAED_PortalEmpleado.Domain.Entities;
using System.Security.Claims;

namespace SAED_PortalEmpleado.Application.Auth.Commands.Login;

public class HandleGoogleCallbackCommandHandler : IRequestHandler<HandleGoogleCallbackCommand, HandleGoogleCallbackResponse>
{
    private readonly IEmployeeRepository _employeeRepository;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly ILogger<HandleGoogleCallbackCommandHandler> _logger;

    public HandleGoogleCallbackCommandHandler(
        IEmployeeRepository employeeRepository,
        IDateTimeProvider dateTimeProvider,
        ILogger<HandleGoogleCallbackCommandHandler> logger)
    {
        _employeeRepository = employeeRepository;
        _dateTimeProvider = dateTimeProvider;
        _logger = logger;
    }

    public async Task<HandleGoogleCallbackResponse> Handle(HandleGoogleCallbackCommand request, CancellationToken cancellationToken)
    {
        var claims = request.Principal?.Claims;
        if (claims == null)
        {
            _logger.LogWarning("No claims found in authentication result");
            return new HandleGoogleCallbackResponse(false, null, "No claims found");
        }

        // Extract claims from Google
        var googleSub = claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier)?.Value;
        var email = claims.FirstOrDefault(c => c.Type == ClaimTypes.Email)?.Value;
        var name = claims.FirstOrDefault(c => c.Type == ClaimTypes.Name)?.Value;
        var picture = claims.FirstOrDefault(c => c.Type == "picture" || c.Type == "urn:google:picture")?.Value;

        if (string.IsNullOrEmpty(googleSub) || string.IsNullOrEmpty(email))
        {
            _logger.LogWarning("Required claims (sub or email) are missing");
            return new HandleGoogleCallbackResponse(false, null, "Required claims are missing");
        }

        // Persist or update employee in database
        var employee = await _employeeRepository.GetByGoogleSubAsync(googleSub, cancellationToken);

        if (employee == null)
        {
            // Create new employee
            employee = new Employee
            {
                Id = Guid.NewGuid(),
                GoogleSub = googleSub,
                Email = email,
                FullName = name ?? email,
                PictureUrl = picture,
                CreatedAt = _dateTimeProvider.UtcNow
            };
            await _employeeRepository.AddAsync(employee, cancellationToken);
            _logger.LogInformation("Creating new employee: {Email}", email);
        }
        else
        {
            // Update existing employee
            employee.Email = email;
            employee.FullName = name ?? email;
            employee.PictureUrl = picture;
            await _employeeRepository.UpdateAsync(employee, cancellationToken);
            _logger.LogInformation("Updating existing employee: {Email}", email);
        }

        // Create claims principal for cookie authentication
        var cookieClaims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, googleSub),
            new Claim(ClaimTypes.Email, email),
            new Claim(ClaimTypes.Name, name ?? email)
        };
        
        if (!string.IsNullOrEmpty(picture))
        {
            cookieClaims.Add(new Claim("picture", picture));
        }

        var identity = new ClaimsIdentity(cookieClaims, "Cookies");
        var principal = new ClaimsPrincipal(identity);

        return new HandleGoogleCallbackResponse(true, principal);
    }
}
