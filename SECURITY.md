# Security Implementation Guide

This document describes the security improvements implemented in the SAED Portal Empleado application.

## Overview

The following security enhancements have been implemented:

1. **Global Exception Handling Middleware**
2. **Secure Cookie Settings**
3. **Antiforgery (CSRF) Protection**
4. **Rate Limiting**
5. **Structured Logging**
6. **Google Token Validation (Issuer and Audience)**
7. **Environment-Based Configuration**

---

## 1. Global Exception Handling Middleware

### Description
A global exception handler middleware catches all unhandled exceptions and returns structured error responses.

### Location
`SAED-PortalEmpleado.Api/Middleware/GlobalExceptionHandlerMiddleware.cs`

### Features
- Catches all unhandled exceptions in the request pipeline
- Maps common exceptions to appropriate HTTP status codes
- Returns structured JSON error responses
- Includes detailed error information in Development environment only
- Includes correlation ID (TraceIdentifier) in all error responses
- Logs all exceptions with full details

### Usage
The middleware is automatically applied to all requests via `app.UseGlobalExceptionHandler()` in `Program.cs`.

### Error Response Format
```json
{
  "error": {
    "message": "An error occurred while processing your request",
    "type": "ExceptionType",
    "statusCode": 500,
    "stackTrace": "... (Development only)",
    "traceId": "correlation-id"
  }
}
```

---

## 2. Secure Cookie Settings

### Description
Enhanced cookie security settings for authentication cookies.

### Configuration
Cookies are configured with the following security settings:

- **HttpOnly**: `true` - Prevents JavaScript access to cookies
- **Secure**: `true` (Production) / `false` (Development) - Requires HTTPS
- **SameSite**: `Strict` (Production) / `Lax` (Development) - Prevents CSRF attacks
- **MaxAge**: 86400 seconds (24 hours)
- **Cookie Name**: `.SAED.PortalEmpleado.Auth`

### Configuration Location
Settings are defined in `appsettings.json` under `SecuritySettings:Cookie`:

```json
{
  "SecuritySettings": {
    "Cookie": {
      "HttpOnly": true,
      "Secure": true,
      "SameSite": "Strict",
      "MaxAge": 86400
    }
  }
}
```

### Environment Overrides
- **Development**: `Secure` is set to `false` to allow HTTP testing
- **Production**: All security settings are enforced

---

## 3. Antiforgery (CSRF) Protection

### Description
Cross-Site Request Forgery (CSRF) protection using ASP.NET Core's built-in antiforgery system.

### Configuration
```csharp
builder.Services.AddAntiforgery(options =>
{
    options.HeaderName = "X-CSRF-TOKEN";
    options.Cookie.Name = "X-CSRF-TOKEN";
    options.Cookie.HttpOnly = true;
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always; // Production
    options.Cookie.SameSite = SameSiteMode.Strict;
});
```

### Protected Endpoints
The following endpoints require CSRF tokens:

- `POST /api/auth/logout`
- `POST /api/employees`
- `PUT /api/employees/{id}`
- `DELETE /api/employees/{id}`

### Usage

#### Step 1: Get CSRF Token
```http
GET /api/auth/csrf-token
```

Response:
```json
{
  "token": "CfDJ8..."
}
```

#### Step 2: Include Token in Request
Include the token in the `X-CSRF-TOKEN` header:

```http
POST /api/auth/logout
X-CSRF-TOKEN: CfDJ8...
```

Or in the request body/form as a field named `__RequestVerificationToken`.

---

## 4. Rate Limiting

### Description
Rate limiting protects the API from abuse and DDoS attacks by limiting the number of requests per client.

### Implementation
Three rate limiting strategies are implemented:

#### Global Rate Limiter (Applied to All Endpoints)
- **Type**: Sliding Window
- **Limit**: 200 requests per minute per IP address
- **Window**: 1 minute with 6 segments
- **Status Code**: 429 (Too Many Requests) when limit is exceeded

#### Named Rate Limiters

**Fixed Window (`"fixed"`)**
- **Limit**: 100 requests per minute
- **Window**: 1 minute
- **Queue**: 2 requests

**Sliding Window (`"sliding"`)**
- **Limit**: 100 requests per minute
- **Window**: 1 minute with 6 segments
- **Queue**: 2 requests

### Configuration
Rate limiting settings are configured in `appsettings.json`:

```json
{
  "RateLimiting": {
    "FixedWindow": {
      "PermitLimit": 100,
      "Window": "00:01:00"
    },
    "SlidingWindow": {
      "PermitLimit": 100,
      "Window": "00:01:00",
      "SegmentsPerWindow": 6
    }
  }
}
```

### Applying Rate Limiters to Specific Endpoints
To apply a named rate limiter to an endpoint, use the `[EnableRateLimiting]` attribute:

```csharp
[HttpGet]
[EnableRateLimiting("sliding")]
public async Task<ActionResult<IEnumerable<Employee>>> GetEmployees()
{
    // ...
}
```

---

## 5. Structured Logging

### Description
Structured logging using Serilog provides consistent, queryable logs with rich context.

### Features
- JSON-formatted logs for easy parsing
- Console and file logging sinks
- Log rotation (daily, 10MB max, 30 days retention in Production)
- Request/response logging with timing
- Correlation IDs for request tracking
- Enrichment with machine name, thread ID, environment name
- User agent and remote IP address logging

### Configuration
Serilog is configured in `appsettings.json`:

```json
{
  "Serilog": {
    "MinimumLevel": {
      "Default": "Information",
      "Override": {
        "Microsoft": "Warning",
        "Microsoft.AspNetCore": "Warning",
        "Microsoft.EntityFrameworkCore.Database.Command": "Warning"
      }
    },
    "WriteTo": [
      {
        "Name": "Console",
        "Args": {
          "outputTemplate": "[{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz}] [{Level:u3}] [{SourceContext}] {Message:lj}{NewLine}{Exception}"
        }
      },
      {
        "Name": "File",
        "Args": {
          "path": "logs/portal-empleado-.log",
          "rollingInterval": "Day",
          "rollOnFileSizeLimit": true,
          "fileSizeLimitBytes": 10485760,
          "retainedFileCountLimit": 30
        }
      }
    ],
    "Enrich": [ "FromLogContext", "WithMachineName", "WithThreadId", "WithEnvironmentName" ]
  }
}
```

### Request Logging Middleware
Location: `SAED-PortalEmpleado.Api/Middleware/RequestLoggingMiddleware.cs`

Features:
- Generates or uses existing correlation IDs (from `X-Correlation-ID` header)
- Logs all HTTP requests with method, path, status code, and duration
- Adds correlation ID to response headers
- Enriches logs with user agent and remote IP address

### Log Output Example
```
[2024-01-15 10:30:45.123 -05:00] [INF] [SAED_PortalEmpleado.Api.Middleware.RequestLoggingMiddleware] HTTP GET /api/employees responded 200 in 45ms {CorrelationId="abc123", UserAgent="Mozilla/5.0...", RemoteIpAddress="192.168.1.100"}
```

### Correlation IDs
Every request is assigned a correlation ID that can be used to trace the request through the entire system:

1. Client can send `X-Correlation-ID` header
2. If not provided, the system generates one using `HttpContext.TraceIdentifier`
3. The correlation ID is returned in the `X-Correlation-ID` response header
4. All logs for that request include the correlation ID

---

## 6. Google Token Validation

### Description
Enhanced validation of Google OAuth tokens to ensure they come from the expected issuer and are intended for this application.

### Configuration
Valid issuers and audiences are configured in `appsettings.json`:

```json
{
  "Authentication": {
    "Google": {
      "ClientId": "your-client-id.apps.googleusercontent.com",
      "ClientSecret": "your-client-secret",
      "ValidAudiences": [
        "your-client-id.apps.googleusercontent.com"
      ],
      "ValidIssuers": [
        "https://accounts.google.com"
      ]
    }
  }
}
```

### Validation Logic
During the `OnCreatingTicket` event in Google authentication:

1. **Issuer Validation**: Verifies the token issuer (`iss` claim) matches `https://accounts.google.com`
2. **Audience Validation**: Ensures the ClientId is in the list of valid audiences
3. **Logging**: Logs warnings for invalid tokens
4. **Exception**: Throws `UnauthorizedAccessException` for invalid issuer

### Security Benefits
- Prevents token replay attacks from other applications
- Ensures tokens are issued by Google, not a malicious third party
- Validates tokens are intended for this application

---

## 7. Environment-Based Configuration

### Description
Separate configuration files for different environments with appropriate security settings for each.

### Configuration Files

#### `appsettings.json` (Base Configuration)
- Contains default settings for all environments
- Includes structured logging configuration
- Defines rate limiting policies
- Sets cookie security defaults

#### `appsettings.Development.json`
- Overrides for local development
- Less restrictive security settings (e.g., `Secure: false` for cookies)
- More verbose logging (`Debug` level)
- Uses LocalDB or local SQL Server

#### `appsettings.Production.json`
- Production-ready security settings
- Strict cookie settings (`Secure: true`, `SameSite: Strict`)
- Reduced logging verbosity
- Connection string template for production database
- Warning about using environment variables or Key Vault

### Environment Selection
The environment is determined by the `ASPNETCORE_ENVIRONMENT` environment variable:

```bash
# Development
export ASPNETCORE_ENVIRONMENT=Development

# Production
export ASPNETCORE_ENVIRONMENT=Production
```

### Best Practices
1. **Never commit secrets**: Use User Secrets (Development) or Azure Key Vault (Production)
2. **Environment Variables**: Use for production secrets
3. **AllowedHosts**: Restrict to specific domains in production
4. **Connection Strings**: Use encrypted connections in production (`Encrypt=True`)

---

## Production Configuration Notes

⚠️ **IMPORTANT**: The `appsettings.Production.json` file contains template values only. 

**Never commit real credentials to source control!**

For production deployments:
- Use **Azure Key Vault** or similar secret management service
- Use **Environment Variables** for sensitive configuration
- Use **Managed Identities** for Azure resources
- Configure secrets through your deployment pipeline

Example using environment variables:
```bash
export ConnectionStrings__DefaultConnection="Server=prod-server;Database=..."
export Authentication__Google__ClientId="real-client-id"
export Authentication__Google__ClientSecret="real-client-secret"
```

---

## Security Checklist

- [x] Global exception handling prevents information leakage
- [x] Secure cookies (HttpOnly, Secure, SameSite) prevent XSS and CSRF
- [x] Antiforgery tokens protect state-changing operations
- [x] Rate limiting prevents abuse and DDoS attacks
- [x] Structured logging provides audit trail
- [x] Correlation IDs enable request tracing
- [x] Google token validation prevents token replay attacks
- [x] Environment-based configuration separates dev and prod settings
- [x] HTTPS enforced via `UseHttpsRedirection()`
- [x] Authorization required on all endpoints except login
- [x] Sensitive information hidden in production error responses
- [x] Connection strings encrypted in production

---

## Additional Security Recommendations

### For Production Deployment

1. **Secrets Management**
   - Use Azure Key Vault or similar for storing secrets
   - Configure managed identities for Azure resources
   - Never commit production secrets to source control

2. **HTTPS/TLS**
   - Use valid SSL/TLS certificates (not self-signed)
   - Enable HSTS (HTTP Strict Transport Security)
   - Consider Certificate Pinning for mobile apps

3. **Database Security**
   - Use connection string encryption
   - Enable SQL Server encryption (TDE)
   - Use least-privilege database accounts
   - Implement database auditing

4. **Additional Headers**
   - Add Security Headers middleware:
     - X-Content-Type-Options: nosniff
     - X-Frame-Options: DENY
     - Content-Security-Policy
     - X-XSS-Protection: 1; mode=block

5. **Monitoring and Alerts**
   - Set up alerts for:
     - Failed authentication attempts
     - Rate limit violations
     - Unhandled exceptions
     - Suspicious IP addresses
   - Integrate with Azure Application Insights or similar

6. **Regular Security Updates**
   - Keep NuGet packages updated
   - Subscribe to security advisories
   - Perform regular security audits
   - Implement automated vulnerability scanning

---

## Testing Security Features

### 1. Test Global Exception Handler
```bash
# Trigger an error and verify structured error response
curl -X GET https://localhost:7079/api/employees/invalid-guid
```

### 2. Test Rate Limiting
```bash
# Make 201+ requests in 1 minute to trigger rate limit
for i in {1..201}; do curl https://localhost:7079/api/auth/me; done
```

### 3. Test CSRF Protection
```bash
# Without token - should fail with 400
curl -X POST https://localhost:7079/api/auth/logout

# With token - should succeed
TOKEN=$(curl https://localhost:7079/api/auth/csrf-token | jq -r .token)
curl -X POST https://localhost:7079/api/auth/logout -H "X-CSRF-TOKEN: $TOKEN"
```

### 4. Test Secure Cookies
```bash
# Verify cookie has HttpOnly, Secure, and SameSite attributes
curl -v https://localhost:7079/api/auth/login
```

### 5. Test Logging
```bash
# Check logs for correlation IDs and structured format
cat logs/portal-empleado-*.log
```

---

## Troubleshooting

### Rate Limit 429 Errors
- Wait for the rate limit window to reset (typically 1 minute)
- Check if you're using the correct rate limit policy
- Adjust rate limit settings in `appsettings.json` if needed

### CSRF Token Validation Fails
- Ensure you're getting a fresh token from `/api/auth/csrf-token`
- Verify the token is included in the `X-CSRF-TOKEN` header
- Check that cookies are enabled and not blocked by CORS

### Logs Not Appearing
- Check the `logs` directory exists and is writable
- Verify Serilog configuration in `appsettings.json`
- Check minimum log level settings

### Cookie Not Secure Warning
- This is expected in Development with HTTP
- In Production, ensure HTTPS is properly configured
- Check `SecuritySettings:Cookie:Secure` setting

---

## References

- [ASP.NET Core Security](https://learn.microsoft.com/en-us/aspnet/core/security/)
- [Serilog Documentation](https://serilog.net/)
- [ASP.NET Core Rate Limiting](https://learn.microsoft.com/en-us/aspnet/core/performance/rate-limit)
- [OWASP Top 10](https://owasp.org/www-project-top-ten/)
- [Google OAuth 2.0](https://developers.google.com/identity/protocols/oauth2)
