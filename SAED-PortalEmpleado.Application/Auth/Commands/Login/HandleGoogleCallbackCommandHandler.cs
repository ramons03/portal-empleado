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
        var cuil = claims.FirstOrDefault(c => c.Type == "cuil" || c.Type == "urn:saed:cuil")?.Value;
        if (string.IsNullOrWhiteSpace(cuil))
        {
            cuil = request.CuilFromDirectory;
        }

        if (string.IsNullOrEmpty(googleSub) || string.IsNullOrEmpty(email))
        {
            _logger.LogWarning("Required claims (sub or email) are missing");
            return new HandleGoogleCallbackResponse(false, null, "Required claims are missing");
        }

        // Persist or update employee in database
        var employee = await _employeeRepository.GetByGoogleSubAsync(googleSub, cancellationToken);
        var isNewEmployee = false;

        if (employee == null)
        {
            // Fallback to email to avoid duplicate rows when sub changes or dev login seeds differ.
            employee = await _employeeRepository.GetByEmailAsync(email, cancellationToken);

            if (employee != null)
            {
                _logger.LogInformation(
                    "Relinking existing employee by email {Email} from sub {ExistingSub} to {NewSub}",
                    employee.Email,
                    employee.GoogleSub,
                    googleSub);
            }
        }

        if (employee == null)
        {
            // Create new employee
            employee = new Employee
            {
                Id = Guid.NewGuid(),
                GoogleSub = googleSub,
                Email = email,
                FullName = name ?? email,
                Cuil = string.IsNullOrWhiteSpace(cuil) ? null : cuil,
                PictureUrl = picture,
                CreatedAt = _dateTimeProvider.UtcNow
            };
            isNewEmployee = true;
        }
        else
        {
            // Always keep profile data and sub in sync with latest login identity.
            employee.GoogleSub = googleSub;
            employee.Email = email;
            employee.FullName = name ?? email;
            if (!string.IsNullOrWhiteSpace(cuil))
            {
                employee.Cuil = cuil;
            }
            employee.PictureUrl = picture;
        }

        if (isNewEmployee)
        {
            await _employeeRepository.AddAsync(employee, cancellationToken);
            _logger.LogInformation("Creating new employee: {Email}", email);
        }
        else
        {
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
        if (!string.IsNullOrWhiteSpace(cuil))
        {
            cookieClaims.Add(new Claim("cuil", cuil));
        }

        var identity = new ClaimsIdentity(cookieClaims, "Cookies");
        var principal = new ClaimsPrincipal(identity);

        return new HandleGoogleCallbackResponse(true, principal);
    }
}
