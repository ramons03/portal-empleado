# Portal Empleado - SAED

Full-stack employee portal with .NET 8 backend using Clean Architecture and BFF pattern, and React + TypeScript frontend.

## 🏗️ Architecture

This solution implements **Backend for Frontend (BFF)** architecture with Clean Architecture principles:

### Backend Structure
```
SAED-PortalEmpleado/
├── SAED-PortalEmpleado.Domain/          # Core business entities
│   └── Entities/
│       └── Employee.cs
├── SAED-PortalEmpleado.Application/     # Business logic with MediatR
│   ├── Auth/
│   │   ├── Commands/                    # Write operations
│   │   └── Queries/                     # Read operations
│   └── Common/
│       └── Interfaces/                  # Abstractions
├── SAED-PortalEmpleado.Infrastructure/  # Data access & repositories
│   ├── Persistence/
│   │   ├── ApplicationDbContext.cs
│   │   └── Migrations/
│   └── Repositories/
│       └── EmployeeRepository.cs
└── SAED-PortalEmpleado.Api/            # API endpoints & services
    ├── Controllers/
    │   ├── AuthController.cs
    │   └── EmployeesController.cs
    └── Services/
        ├── CurrentUserService.cs
        └── DateTimeProvider.cs
```

### Frontend Structure
```
frontend/portal-empleado/
├── src/
│   ├── pages/                           # Page components
│   │   ├── Login.tsx                    # Google OAuth login
│   │   ├── Home.tsx                     # Main dashboard
│   │   ├── Recibos.tsx                  # Payroll receipts
│   │   └── Vacaciones.tsx               # Vacation requests
│   ├── services/
│   │   └── auth.ts                      # Authentication API
│   ├── types/
│   │   └── index.ts                     # TypeScript definitions
│   └── App.tsx                          # Main app with routing
└── package.json
```

## 🚀 Technologies

### Backend
- **.NET 8.0** - Latest LTS framework
- **MediatR 14.0.0** - CQRS pattern implementation
- **Entity Framework Core 8.0.11** - ORM for data access
- **SQL Server** - Database provider
- **Swagger/OpenAPI** - API documentation
- **Google OAuth** - Authentication provider
- **Serilog** - Structured logging

### Frontend
- **React 18** - UI framework
- **TypeScript** - Type-safe JavaScript
- **Vite 7** - Build tool and dev server
- **React Router** - Client-side routing

## 📋 Key Features

### Backend (BFF)
- **Clean Architecture** with proper layer separation
- **CQRS Pattern** using MediatR for commands and queries
- **Repository Pattern** for data access abstraction
- **Response Caching** for improved performance
- **Rate Limiting** to prevent abuse
- **CORS Configuration** for frontend integration
- **CSRF Protection** for state-changing operations
- **Structured Logging** with Serilog
- **Microservice-Ready** with placeholder interfaces for future services

### Frontend
- **Modern React** with functional components and hooks
- **Type-Safe** with TypeScript
- **Client-Side Routing** with React Router
- **Responsive Design** with modern CSS
- **Authentication Flow** with automatic 401 handling
- **CSRF Protection** on logout and mutations

## 🛠️ Setup & Running

### Prerequisites

- .NET 8 SDK
- SQL Server (LocalDB or full instance)
- Node.js 18+ and npm

### Build the solution

```bash
dotnet build
```

### Update the database

```bash
cd SAED-PortalEmpleado.Api
dotnet ef database update
```

### Run the Backend API

```bash
cd SAED-PortalEmpleado.Api
dotnet run
```

The API will be available at:
- HTTPS: https://localhost:7079
- Swagger UI: https://localhost:7079/swagger

### Run the Frontend

```bash
cd frontend/portal-empleado
npm install
npm run dev
```

The frontend will be available at:
- http://localhost:5173

## 🔒 Configuration

### Backend Configuration

#### Connection Strings

Update the connection string in `appsettings.json` or `appsettings.Development.json`:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=YOUR_SERVER;Database=SAED_PortalEmpleado;User Id=YOUR_USER;Password=YOUR_PASSWORD;TrustServerCertificate=True"
  }
}
```

**⚠️ Security Note:** Never commit real credentials to source control. Use User Secrets for development:

```bash
cd SAED-PortalEmpleado.Api
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "YOUR_CONNECTION_STRING"
```

#### Google OAuth Configuration

Set up Google OAuth credentials as described in [AUTHENTICATION.md](AUTHENTICATION.md):

```bash
dotnet user-secrets set "Authentication:Google:ClientId" "your-client-id"
dotnet user-secrets set "Authentication:Google:ClientSecret" "your-client-secret"
```

#### CORS Configuration (Production)

For production, update allowed origins in `appsettings.Production.json`:

```json
{
  "Cors": {
    "AllowedOrigins": ["https://your-production-domain.com"]
  }
}
```

## 📚 API Endpoints

### Authentication Endpoints

- `GET /api/auth/login` - Initiates Google OAuth login flow (public)
- `GET /api/auth/google-callback` - Handles OAuth callback (public)
- `POST /api/auth/logout` - Logs out current user (requires auth & CSRF token)
- `GET /api/auth/me` - Returns current user info (requires auth, cached 5 min)
- `GET /api/auth/csrf-token` - Gets CSRF token for protected operations (public)

### Employee Endpoints

- `GET /api/employees` - Get all employees (requires auth)
- `GET /api/employees/{id}` - Get employee by ID (requires auth)
- `POST /api/employees` - Create new employee (requires auth & CSRF token)
- `PUT /api/employees/{id}` - Update employee (requires auth & CSRF token)
- `DELETE /api/employees/{id}` - Delete employee (requires auth & CSRF token)

## 🎨 Frontend Pages

### Login (`/login`)
- Google OAuth login button
- Redirects to backend `/api/auth/login`
- Clean, modern design with gradient background

### Home (`/`)
- Main dashboard after authentication
- Displays user profile information
- Quick action cards for Recibos and Vacaciones
- Navigation and logout functionality
- Automatic redirect to `/login` if not authenticated (401)

### Recibos (`/recibos`)
- Placeholder for payroll receipts feature
- Will integrate with Nomina microservice

### Vacaciones (`/vacaciones`)
- Placeholder for vacation requests feature
- Will integrate with Asistencia microservice

## 🧪 Testing

### Backend
Access Swagger UI at `https://localhost:7079/swagger` to test API endpoints interactively.

### Frontend
The frontend includes:
- Automatic 401 handling with redirect to login
- CSRF token fetching for protected operations
- Cookie-based authentication with `credentials: 'include'`

## 📝 Database Migrations

### Create a new migration

```bash
cd SAED-PortalEmpleado.Api
dotnet ef migrations add MigrationName --project ../SAED-PortalEmpleado.Infrastructure
```

### Apply migrations

```bash
dotnet ef database update
```

## 📖 Additional Documentation

- **[AUTHENTICATION.md](AUTHENTICATION.md)** - Detailed Google OAuth setup guide
- **[BFF_IMPLEMENTATION.md](BFF_IMPLEMENTATION.md)** - Complete BFF architecture documentation
- **[SECURITY.md](SECURITY.md)** - Security features and best practices

## 🔄 Architecture Benefits

### Clean Architecture
- **Separation of Concerns**: Each layer has clear responsibilities
- **Dependency Rule**: Dependencies point inward (Domain ← Application ← Infrastructure ← API)
- **Testability**: Easy to unit test handlers independently
- **Maintainability**: Changes in one layer don't affect others

### CQRS with MediatR
- **Command/Query Separation**: Clear distinction between reads and writes
- **Single Responsibility**: Each handler does one thing
- **Pipeline Behavior**: Easy to add cross-cutting concerns (logging, validation)

### Repository Pattern
- **Data Access Abstraction**: Controllers don't know about DbContext
- **Testability**: Easy to mock data access
- **Flexibility**: Can swap implementations (e.g., different ORM)

## 🚀 Future Enhancements

### Backend
- ✅ ~~Implement CQRS with MediatR~~ (Complete)
- ✅ ~~Add authentication and authorization (OAuth2/Google)~~ (Complete)
- ✅ ~~Add response caching~~ (Complete)
- ✅ ~~Repository pattern~~ (Complete)
- Add unit and integration tests
- Add validation using FluentValidation
- Implement DTOs and AutoMapper
- Add API versioning
- Implement health checks
- Integrate with Nomina microservice
- Integrate with Asistencia microservice

### Frontend
- Add loading states and error boundaries
- Implement unit tests with Vitest
- Add E2E tests with Playwright
- Improve accessibility (ARIA labels, keyboard navigation)
- Add dark mode support
- Implement proper error handling UI