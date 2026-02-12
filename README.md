# Portal Empleado - SAED

.NET 8 solution using Clean Architecture for employee portal management.

## 🏗️ Architecture

This solution follows Clean Architecture principles with the following layers:

```
SAED-PortalEmpleado/
├── SAED-PortalEmpleado.Domain/          # Core business entities
│   └── Entities/
│       └── Employee.cs
├── SAED-PortalEmpleado.Application/     # Business logic & interfaces
│   └── DependencyInjection.cs
├── SAED-PortalEmpleado.Infrastructure/  # Data access & external services
│   └── Persistence/
│       ├── ApplicationDbContext.cs
│       └── Migrations/
└── SAED-PortalEmpleado.Api/            # API endpoints & presentation
    └── Controllers/
        └── EmployeesController.cs
```

## 🚀 Technologies

- **.NET 8.0** - Latest LTS framework
- **Entity Framework Core 8.0.11** - ORM for data access
- **SQL Server** - Database provider
- **Swagger/OpenAPI** - API documentation
- **ASP.NET Core Web API** - RESTful API framework

## 📋 Employee Entity

The Employee entity includes the following properties:

- `Id` (Guid) - Primary key
- `GoogleSub` (string) - Google OAuth subject identifier (unique)
- `Email` (string) - Employee email (unique)
- `FullName` (string) - Full name
- `PictureUrl` (string, optional) - Profile picture URL
- `CreatedAt` (DateTime) - Creation timestamp

## 🛠️ Setup & Running

### Prerequisites

- .NET 8 SDK
- SQL Server (LocalDB or full instance)

### Build the solution

```bash
dotnet build
```

### Update the database

```bash
cd SAED-PortalEmpleado.Api
dotnet ef database update
```

### Run the API

```bash
cd SAED-PortalEmpleado.Api
dotnet run
```

The API will be available at:
- HTTP: http://localhost:5011
- HTTPS: https://localhost:7079
- Swagger UI: https://localhost:7079/swagger

## 🔒 Configuration

### Connection Strings

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

## 📚 API Endpoints

All endpoints are available under `/api/employees`:

- `GET /api/employees` - Get all employees
- `GET /api/employees/{id}` - Get employee by ID
- `POST /api/employees` - Create new employee
- `PUT /api/employees/{id}` - Update employee
- `DELETE /api/employees/{id}` - Delete employee

## 🧪 Testing

Access Swagger UI at `/swagger` to test the API endpoints interactively.

## 📝 Migrations

### Create a new migration

```bash
cd SAED-PortalEmpleado.Api
dotnet ef migrations add MigrationName --project ../SAED-PortalEmpleado.Infrastructure
```

### Apply migrations

```bash
dotnet ef database update
```

## 🔄 Future Improvements

- Implement Repository pattern or CQRS with MediatR
- Add authentication and authorization (OAuth2/Google)
- Add unit and integration tests
- Add validation using FluentValidation
- Implement DTOs and AutoMapper
- Add logging with Serilog
- Add API versioning
- Implement health checks