using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SAED_PortalEmpleado.Application.Common.Interfaces;
using SAED_PortalEmpleado.Infrastructure.Persistence;
using SAED_PortalEmpleado.Infrastructure.Repositories;
using SAED_PortalEmpleado.Infrastructure.Services;

namespace SAED_PortalEmpleado.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");

        var databaseProvider = configuration["DatabaseProvider"] ?? "SqlServer";

        services.AddDbContext<ApplicationDbContext>(options =>
        {
            if (string.Equals(databaseProvider, "Sqlite", StringComparison.OrdinalIgnoreCase))
            {
                options.UseSqlite(connectionString);
                return;
            }

            options.UseSqlServer(connectionString);
        });

        // Register repositories
        services.AddScoped<IEmployeeRepository, EmployeeRepository>();
        services.AddSingleton<IReceiptCatalogCache, SqliteReceiptCatalogCache>();
        services.AddSingleton<IReceiptSourceAws, AwsReceiptSource>();
        services.AddSingleton<IReceiptPeriodLock, ReceiptPeriodLock>();

        return services;
    }
}
