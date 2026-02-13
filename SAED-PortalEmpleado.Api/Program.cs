using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.RateLimiting;
using SAED_PortalEmpleado.Api.Middleware;
using SAED_PortalEmpleado.Api.Services;
using SAED_PortalEmpleado.Application;
using SAED_PortalEmpleado.Application.Common.Interfaces;
using SAED_PortalEmpleado.Infrastructure;
using Serilog;
using System.Threading.RateLimiting;

// Determine the current environment
var environmentName = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production";

// Configure Serilog
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(new ConfigurationBuilder()
        .SetBasePath(Directory.GetCurrentDirectory())
        .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
        .AddJsonFile($"appsettings.{environmentName}.json", optional: true, reloadOnChange: true)
        .AddEnvironmentVariables()
        .Build())
    .CreateLogger();

try
{
    Log.Information("Starting SAED Portal Empleado API");

    var builder = WebApplication.CreateBuilder(args);

    // Use Serilog for logging
    builder.Host.UseSerilog();

    // Add services to the container
    builder.Services.AddControllers();
    builder.Services.AddRazorPages();

    // Add HttpContextAccessor (required for CurrentUserService)
    builder.Services.AddHttpContextAccessor();

    // Add Application and Infrastructure layers
    builder.Services.AddApplication();
    builder.Services.AddInfrastructure(builder.Configuration);
    
    // Add Response Caching
    builder.Services.AddResponseCaching();
    
    // Register CurrentUserService
    builder.Services.AddScoped<ICurrentUserService, CurrentUserService>();
    
    // Register DateTimeProvider
    builder.Services.AddSingleton<IDateTimeProvider, DateTimeProvider>();
    
    // Add CORS for frontend
    // NOTE: In production, update the allowed origins to match your production domain
    var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() 
        ?? new[] { "http://localhost:5173", "http://localhost:3000" };
    
    builder.Services.AddCors(options =>
    {
        options.AddPolicy("AllowFrontend", policy =>
        {
            policy.WithOrigins(allowedOrigins) // Development origins - configure for production
                  .AllowAnyHeader()
                  .AllowAnyMethod()
                  .AllowCredentials(); // Required for cookies
        });
    });

    // Add Antiforgery protection
    builder.Services.AddAntiforgery(options =>
    {
        options.HeaderName = "X-CSRF-TOKEN";
        options.Cookie.Name = "X-CSRF-TOKEN";
        options.Cookie.HttpOnly = true;
        options.Cookie.SecurePolicy = builder.Environment.IsDevelopment() 
            ? CookieSecurePolicy.SameAsRequest 
            : CookieSecurePolicy.Always;
        options.Cookie.SameSite = SameSiteMode.Strict;
    });

    // Get security settings from configuration
    var securitySettings = builder.Configuration.GetSection("SecuritySettings:Cookie");
    var cookieHttpOnly = securitySettings.GetValue<bool>("HttpOnly", true);
    var cookieSecure = securitySettings.GetValue<bool>("Secure", !builder.Environment.IsDevelopment());
    var cookieSameSite = Enum.Parse<SameSiteMode>(securitySettings.GetValue<string>("SameSite") ?? "Lax");
    var cookieMaxAge = securitySettings.GetValue<int>("MaxAge", 86400);

    // Add Authentication with secure cookie settings
    builder.Services.AddAuthentication(options =>
    {
        options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = GoogleDefaults.AuthenticationScheme;
    })
    .AddCookie(options =>
    {
        options.LoginPath = "/api/auth/login";
        options.LogoutPath = "/api/auth/logout";
        options.AccessDeniedPath = "/";
        options.ExpireTimeSpan = TimeSpan.FromSeconds(cookieMaxAge);
        options.SlidingExpiration = true;
        
        // Secure cookie settings
        options.Cookie.HttpOnly = cookieHttpOnly;
        options.Cookie.SecurePolicy = cookieSecure ? CookieSecurePolicy.Always : CookieSecurePolicy.SameAsRequest;
        options.Cookie.SameSite = cookieSameSite;
        options.Cookie.Name = ".SAED.PortalEmpleado.Auth";
        options.Cookie.MaxAge = TimeSpan.FromSeconds(cookieMaxAge);
    })
    .AddGoogle(options =>
    {
        options.ClientId = builder.Configuration["Authentication:Google:ClientId"] ?? throw new InvalidOperationException("Google ClientId not configured");
        options.ClientSecret = builder.Configuration["Authentication:Google:ClientSecret"] ?? throw new InvalidOperationException("Google ClientSecret not configured");
        options.SaveTokens = true;
        options.Scope.Add("profile");
        options.Scope.Add("email");
        
        // Validate Google token issuer and audience
        var validAudiences = builder.Configuration.GetSection("Authentication:Google:ValidAudiences").Get<string[]>();
        var validIssuers = builder.Configuration.GetSection("Authentication:Google:ValidIssuers").Get<string[]>();
        
        options.Events.OnCreatingTicket = context =>
        {
            // Validate token issuer
            if (validIssuers?.Any() == true)
            {
                var issuer = context.Principal?.FindFirst("iss")?.Value;
                if (!string.IsNullOrEmpty(issuer) && !validIssuers.Contains(issuer))
                {
                    Log.Warning("Invalid token issuer: {Issuer}", issuer);
                    throw new UnauthorizedAccessException($"Invalid token issuer: {issuer}");
                }
            }
            
            // Validate token audience (ClientId should match)
            if (validAudiences?.Any() == true)
            {
                // Google authentication validates audience automatically against ClientId
                // This is an additional check for stricter validation
                var clientId = builder.Configuration["Authentication:Google:ClientId"];
                if (!string.IsNullOrEmpty(clientId) && !validAudiences.Contains(clientId))
                {
                    Log.Warning("ClientId not in valid audiences list");
                }
            }
            
            return Task.CompletedTask;
        };
    });

    // Add Authorization
    builder.Services.AddAuthorization();

    // Add Rate Limiting
    builder.Services.AddRateLimiter(options =>
    {
        // Fixed window rate limiter
        options.AddFixedWindowLimiter("fixed", opt =>
        {
            opt.PermitLimit = builder.Configuration.GetValue<int>("RateLimiting:FixedWindow:PermitLimit", 100);
            opt.Window = TimeSpan.Parse(builder.Configuration.GetValue<string>("RateLimiting:FixedWindow:Window") ?? "00:01:00");
            opt.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
            opt.QueueLimit = 2;
        });

        // Sliding window rate limiter (more accurate)
        options.AddSlidingWindowLimiter("sliding", opt =>
        {
            opt.PermitLimit = builder.Configuration.GetValue<int>("RateLimiting:SlidingWindow:PermitLimit", 100);
            opt.Window = TimeSpan.Parse(builder.Configuration.GetValue<string>("RateLimiting:SlidingWindow:Window") ?? "00:01:00");
            opt.SegmentsPerWindow = builder.Configuration.GetValue<int>("RateLimiting:SlidingWindow:SegmentsPerWindow", 6);
            opt.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
            opt.QueueLimit = 2;
        });

        // Global rate limiter - applied to all endpoints
        options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
        {
            // Rate limit by IP address
            var remoteIp = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
            
            return RateLimitPartition.GetSlidingWindowLimiter(remoteIp, _ => new SlidingWindowRateLimiterOptions
            {
                PermitLimit = 200,
                Window = TimeSpan.FromMinutes(1),
                SegmentsPerWindow = 6,
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                QueueLimit = 0
            });
        });

        options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    });

    // Add Swagger/OpenAPI
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen(c =>
    {
        c.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo
        {
            Title = "SAED Portal Empleado API",
            Version = "v1",
            Description = "API for Employee Portal Management"
        });
    });

    var app = builder.Build();

    // Add global exception handler (should be first in pipeline)
    app.UseGlobalExceptionHandler();

    // Add request logging with correlation IDs
    app.UseRequestLogging();

    // Configure the HTTP request pipeline
    if (app.Environment.IsDevelopment())
    {
        app.UseSwagger();
        app.UseSwaggerUI(c =>
        {
            c.SwaggerEndpoint("/swagger/v1/swagger.json", "SAED Portal Empleado API v1");
        });
    }

    app.UseHttpsRedirection();
    
    // Enable CORS
    app.UseCors("AllowFrontend");

    // Enable rate limiting
    app.UseRateLimiter();
    
    // Enable response caching
    app.UseResponseCaching();

    app.UseAuthentication();
    app.UseAuthorization();

    app.MapControllers();
    app.MapRazorPages();

    Log.Information("SAED Portal Empleado API started successfully");
    
    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}
