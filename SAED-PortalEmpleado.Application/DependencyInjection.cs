using Microsoft.Extensions.DependencyInjection;

namespace SAED_PortalEmpleado.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        // Add application services here
        return services;
    }
}
