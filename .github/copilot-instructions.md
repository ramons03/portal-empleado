# Copilot Instructions for SAED Portal Empleado

## Project Overview

This is a full-stack employee portal application built with:
- **Backend**: .NET 8 using Clean Architecture with Backend for Frontend (BFF) pattern
- **Frontend**: React 18 with TypeScript and Vite
- **Database**: SQL Server with Entity Framework Core
- **Authentication**: Google OAuth 2.0 with OpenID Connect

The application provides employee management functionality with secure authentication and modern security practices.

## Architecture

### Backend Structure (Clean Architecture)

```
SAED-PortalEmpleado/
├── Domain/          # Core business entities (Employee)
├── Application/     # Business logic with MediatR (CQRS Commands/Queries)
├── Infrastructure/  # Data access (EF Core, Repositories, DbContext)
└── Api/            # API endpoints, Controllers, Middleware
```

**Key Principles:**
- Dependencies flow inward: Domain ← Application ← Infrastructure ← Api
- Use MediatR for CQRS pattern (Commands for writes, Queries for reads)
- Repository pattern for data access abstraction
- Interface-based abstractions in Application layer

### Frontend Structure

```
frontend/portal-empleado/
├── src/
│   ├── pages/      # React page components (Login, Home, Recibos, Vacaciones)
│   ├── services/   # API service layer
│   ├── types/      # TypeScript type definitions
│   └── App.tsx     # Main app with React Router
```

## Technology Stack

### Backend Dependencies
- .NET 8.0 SDK
- MediatR 14.0.0 (CQRS implementation)
- Entity Framework Core 8.0.11
- Google.Apis.Auth (OAuth validation)
- Serilog (structured logging)
- Swagger/OpenAPI

### Frontend Dependencies
- React 19.2.0
- TypeScript 5.9.3
- React Router 7.13.0
- Vite 7.3.1
- ESLint for linting

## Coding Standards

### C# Backend
- Use **async/await** for all I/O operations
- Follow **Clean Architecture** layers strictly
- Use **MediatR handlers** for business logic (Command/Query pattern)
- Implement **Repository pattern** for data access
- Apply **[Authorize]** attribute to protected endpoints
- Use **[ValidateAntiForgeryToken]** for state-changing operations (POST/PUT/DELETE)
- Follow **Microsoft C# coding conventions**
- Use **nullable reference types** (enabled in all projects)

### TypeScript Frontend
- Use **functional components** with hooks (no class components)
- Define **strict TypeScript interfaces** for all data structures
- Use **async/await** for API calls
- Include **credentials: 'include'** in fetch calls for cookie-based auth
- Handle **401 responses** by redirecting to login
- Fetch and include **CSRF tokens** for mutations

### Security Best Practices
- Never commit secrets (use User Secrets for development)
- Always validate and sanitize user input
- Use HTTPS in production (enforced via middleware)
- Apply rate limiting to prevent abuse
- Include CSRF protection on state-changing endpoints
- Log security events (failed auth, rate limit violations)
- Use secure cookie settings (HttpOnly, Secure, SameSite)

## Build & Test Commands

### Backend
```bash
# Build
dotnet build

# Run migrations
cd SAED-PortalEmpleado.Api
dotnet ef database update

# Run API
cd SAED-PortalEmpleado.Api
dotnet run

# Create new migration
cd SAED-PortalEmpleado.Api
dotnet ef migrations add MigrationName --project ../SAED-PortalEmpleado.Infrastructure
```

### Frontend
```bash
cd frontend/portal-empleado

# Install dependencies
npm install

# Run dev server
npm run dev

# Build for production
npm run build

# Lint
npm run lint
```

## Common Tasks

### Adding a New Entity
1. Create entity in **Domain/Entities**
2. Add DbSet to **Infrastructure/Persistence/ApplicationDbContext.cs**
3. Create repository interface in **Application/Common/Interfaces**
4. Implement repository in **Infrastructure/Repositories**
5. Create migration: `dotnet ef migrations add AddEntityName`
6. Update database: `dotnet ef database update`

### Adding a New API Endpoint
1. Create Command/Query in **Application/{Feature}/Commands** or **Application/{Feature}/Queries**
2. Create handler implementing `IRequestHandler<TRequest, TResponse>`
3. Create controller in **Api/Controllers**
4. Inject `IMediator` and call `await _mediator.Send(command)`
5. Add **[Authorize]** attribute if authentication required
6. Add **[ValidateAntiForgeryToken]** if state-changing (POST/PUT/DELETE)

### Adding a New Frontend Page
1. Create page component in **src/pages**
2. Add route in **App.tsx** using React Router
3. Create service methods in **src/services** for API calls
4. Define TypeScript interfaces in **src/types**
5. Handle authentication (redirect to /login on 401)

## Authentication Flow

1. User navigates to `/api/auth/login`
2. Backend redirects to Google OAuth consent screen
3. Google redirects to `/api/auth/google-callback`
4. Backend validates token (issuer, audience, claims)
5. Backend creates/updates Employee record in database
6. Backend creates authenticated cookie session
7. Frontend stores session cookie (HttpOnly, Secure)
8. Protected API calls include cookie automatically
9. Frontend checks auth status and redirects to /login on 401

**Key Claims from Google:**
- `sub`: Unique Google user ID (stored as GoogleSub)
- `email`: User's email
- `name`: User's full name
- `picture`: Profile picture URL

## Security Features

### Implemented Security Measures
- **Global Exception Handler**: Structured error responses, no information leakage
- **Secure Cookies**: HttpOnly, Secure (prod), SameSite=Strict
- **CSRF Protection**: Antiforgery tokens on POST/PUT/DELETE endpoints
- **Rate Limiting**: Sliding window (200 req/min global, configurable per endpoint)
- **Structured Logging**: Serilog with correlation IDs, file & console output
- **Token Validation**: Google OAuth issuer and audience validation
- **HTTPS Enforcement**: Redirect middleware in production

### Security Checklist for Changes
- [ ] No secrets committed to repository
- [ ] Input validation on all user inputs
- [ ] Authorization checks on protected endpoints
- [ ] CSRF tokens on state-changing operations
- [ ] Proper error handling without information leakage
- [ ] Logging for security events
- [ ] Rate limiting considered for new endpoints

## Configuration

### Environment-Based Settings
- **Development**: `appsettings.Development.json` (less restrictive, verbose logging)
- **Production**: `appsettings.Production.json` (strict security, minimal logging)

### Secrets Management
- **Development**: Use User Secrets (`dotnet user-secrets set "Key" "Value"`)
- **Production**: Use environment variables or Azure Key Vault
- **Never commit** credentials to appsettings.json

### Key Configuration Sections
- `ConnectionStrings:DefaultConnection`: Database connection
- `Authentication:Google:ClientId/ClientSecret`: OAuth credentials
- `SecuritySettings:Cookie`: Cookie security settings
- `RateLimiting`: Rate limit policies
- `Serilog`: Logging configuration
- `Cors:AllowedOrigins`: Frontend origins for CORS

## Testing Strategy

### API Testing
- Use Swagger UI at `https://localhost:7079/swagger`
- Test authentication flow end-to-end
- Verify CSRF protection on mutations
- Check rate limiting with multiple requests
- Validate error responses

### Frontend Testing
- Manually test authentication flow
- Verify 401 handling redirects to login
- Test CSRF token inclusion on logout
- Validate responsive design

## Known Limitations & Future Work

### Backend
- Unit and integration tests needed
- Input validation with FluentValidation
- DTOs and AutoMapper for request/response mapping
- API versioning
- Health checks endpoint
- Integration with Nomina and Asistencia microservices

### Frontend
- Loading states and error boundaries
- Unit tests with Vitest
- E2E tests with Playwright
- Enhanced accessibility (ARIA labels)
- Dark mode support

## Documentation References

- **README.md**: Complete setup and running instructions
- **AUTHENTICATION.md**: Detailed Google OAuth setup guide
- **SECURITY.md**: Comprehensive security features documentation
- **BFF_IMPLEMENTATION.md**: BFF architecture details

## Important Notes

- This is a **Clean Architecture** project - respect layer boundaries
- Use **MediatR** for all business logic - don't put logic in controllers
- Follow **Repository pattern** - don't use DbContext directly in Application layer
- **CSRF tokens required** for POST/PUT/DELETE operations
- **Always use async/await** for database and external API calls
- **Log security events** using Serilog with correlation IDs
- Frontend and backend run on **different ports** (frontend: 5173, API: 7079)

## Questions or Issues?

When unsure:
1. Check existing code patterns in similar features
2. Review documentation files (README, AUTHENTICATION, SECURITY)
3. Follow Clean Architecture and CQRS principles
4. Maintain consistency with existing code style
5. Prioritize security and proper error handling
