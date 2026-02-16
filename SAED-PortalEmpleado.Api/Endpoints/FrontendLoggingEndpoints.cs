using Microsoft.AspNetCore.Mvc;
using Serilog;
using Serilog.Events;

namespace SAED_PortalEmpleado.Api.Endpoints;

/// <summary>
/// Endpoint para recibir logs del frontend React y reenviarlos a Serilog (→ Console/File/Loki).
/// </summary>
public static class FrontendLoggingEndpoints
{
    private static readonly Serilog.ILogger FrontendLogger = Log.ForContext("Source", "frontend");

    private const int MaxBatchSize = 50;

    public static IEndpointRouteBuilder MapFrontendLoggingEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/api/logs", async (HttpContext context) =>
        {
            var entries = await context.Request.ReadFromJsonAsync<List<FrontendLogEntry>>();

            if (entries is null || entries.Count == 0)
                return Results.BadRequest();

            if (entries.Count > MaxBatchSize)
                entries = entries.Take(MaxBatchSize).ToList();

            foreach (var entry in entries)
            {
                var level = ParseLevel(entry.Level);

                FrontendLogger
                    .ForContext("FrontendTimestamp", entry.Timestamp)
                    .ForContext("Url", entry.Properties?.GetValueOrDefault("url"))
                    .ForContext("Properties", entry.Properties, destructureObjects: true)
                    .Write(level, "[Frontend] {Message}", entry.Message);
            }

            return Results.Ok();
        })
        .WithTags("Logging")
        .WithName("PostFrontendLogs")
        .AllowAnonymous();

        return endpoints;
    }

    private static LogEventLevel ParseLevel(string? level)
    {
        return level?.ToLowerInvariant() switch
        {
            "debug" => LogEventLevel.Debug,
            "info" or "information" => LogEventLevel.Information,
            "warning" or "warn" => LogEventLevel.Warning,
            "error" => LogEventLevel.Error,
            "fatal" => LogEventLevel.Fatal,
            _ => LogEventLevel.Information,
        };
    }

    private sealed class FrontendLogEntry
    {
        public string? Level { get; set; }
        public string Message { get; set; } = string.Empty;
        public string? Timestamp { get; set; }
        public Dictionary<string, object>? Properties { get; set; }
    }
}
