using Google.Apis.Admin.Directory.directory_v1;
using Google.Apis.Admin.Directory.directory_v1.Data;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Services;
using Microsoft.Extensions.Options;

namespace SAED_PortalEmpleado.Api.Services;

public interface IGoogleDirectoryCuilService
{
    Task<string?> GetCuilByEmailAsync(string? email);
}

public class GoogleDirectoryCuilService : IGoogleDirectoryCuilService
{
    private readonly ILogger<GoogleDirectoryCuilService> _logger;
    private readonly DirectorySettings _settings;

    public GoogleDirectoryCuilService(
        ILogger<GoogleDirectoryCuilService> logger,
        IOptions<DirectorySettings> settings)
    {
        _logger = logger;
        _settings = settings.Value;
    }

    public async Task<string?> GetCuilByEmailAsync(string? email)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            return null;
        }

        if (string.IsNullOrWhiteSpace(_settings.ServiceAccountJsonPath) ||
            string.IsNullOrWhiteSpace(_settings.AdminUser) ||
            string.IsNullOrWhiteSpace(_settings.CustomSchema) ||
            string.IsNullOrWhiteSpace(_settings.CuilField))
        {
            return null;
        }

        try
        {
            if (!File.Exists(_settings.ServiceAccountJsonPath))
            {
                _logger.LogWarning("Directory API JSON not found at {Path}", _settings.ServiceAccountJsonPath);
                return null;
            }

            var credential = GoogleCredential
                .FromFile(_settings.ServiceAccountJsonPath)
                .CreateScoped(DirectoryService.Scope.AdminDirectoryUserReadonly)
                .CreateWithUser(_settings.AdminUser);

            var service = new DirectoryService(new BaseClientService.Initializer
            {
                HttpClientInitializer = credential,
                ApplicationName = "SAED-PortalEmpleado"
            });

            var request = service.Users.Get(email);
            request.Projection = UsersResource.GetRequest.ProjectionEnum.Full;

            var user = await request.ExecuteAsync();
            var customSchemas = user.CustomSchemas;
            if (customSchemas == null || customSchemas.Count == 0)
            {
                return null;
            }

            if (!customSchemas.TryGetValue(_settings.CustomSchema, out var schemaValues) || schemaValues == null)
            {
                return null;
            }

            if (!schemaValues.TryGetValue(_settings.CuilField, out var cuilObj) || cuilObj == null)
            {
                return null;
            }

            var cuil = cuilObj.ToString();
            return string.IsNullOrWhiteSpace(cuil) ? null : cuil;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to fetch CUIL from Directory API");
            return null;
        }
    }
}

public class DirectorySettings
{
    public string ServiceAccountJsonPath { get; set; } = string.Empty;
    public string AdminUser { get; set; } = string.Empty;
    public string CustomSchema { get; set; } = string.Empty;
    public string CuilField { get; set; } = string.Empty;
}
