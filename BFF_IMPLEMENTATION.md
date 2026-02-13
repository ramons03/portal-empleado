# BFF Architecture Implementation Summary

## Overview

This document describes the refactoring of the Portal Empleado API into a Backend for Frontend (BFF) architecture with proper separation of concerns, using MediatR for command/query handling, and the creation of a React + TypeScript frontend.

## Backend Changes

### 1. Application Layer Refactoring

#### MediatR Integration
- **Package Added**: `MediatR 14.0.0`
- **Purpose**: Implements CQRS (Command Query Responsibility Segregation) pattern
- **Configuration**: Registered in `Application/DependencyInjection.cs`

#### Command/Query Structure

**Authentication Commands:**
- `HandleGoogleCallbackCommand` - Processes Google OAuth callback
- `HandleGoogleCallbackCommandHandler` - Creates/updates employee records after Google authentication

**Authentication Queries:**
- `GetCurrentUserQuery` - Retrieves current authenticated user
- `GetCurrentUserQueryHandler` - Returns user information from database

### 2. Interface Abstractions

#### ICurrentUserService
- **Location**: `Application/Common/Interfaces/ICurrentUserService.cs`
- **Implementation**: `Api/Services/CurrentUserService.cs`
- **Purpose**: Abstracts access to current authenticated user's claims
- **Properties**:
  - `GoogleSub` - Google's unique identifier
  - `Email` - User's email address
  - `FullName` - User's full name
  - `IsAuthenticated` - Authentication status

#### IEmployeeRepository
- **Location**: `Application/Common/Interfaces/IEmployeeRepository.cs`
- **Implementation**: `Infrastructure/Repositories/EmployeeRepository.cs`
- **Purpose**: Abstracts data access for Employee entity
- **Methods**:
  - `GetByGoogleSubAsync()` - Find employee by Google Sub
  - `GetByIdAsync()` - Find employee by ID
  - `GetAllAsync()` - Get all employees
  - `AddAsync()` - Create new employee
  - `UpdateAsync()` - Update existing employee
  - `DeleteAsync()` - Delete employee

### 3. Future Microservice Integration

#### Placeholder Interfaces
- **INominaService** - For payroll microservice integration
- **IAsistenciaService** - For attendance/vacation microservice integration
- **Location**: `Application/Common/Interfaces/`
- **Status**: Ready for future implementation

### 4. Refactored AuthController

**Changes:**
- Removed direct `ApplicationDbContext` dependency
- Integrated `IMediator` for command/query handling
- Simplified controller methods to focus on HTTP concerns
- Added response caching to `/me` endpoint (5-minute cache per user)

**Key Endpoints:**
- `GET /api/auth/login` - Initiates Google OAuth flow
- `GET /api/auth/google-callback` - Handles OAuth callback
- `POST /api/auth/logout` - Logs out user (with CSRF protection)
- `GET /api/auth/me` - Returns current user (with response caching)
- `GET /api/auth/csrf-token` - Provides CSRF token

### 5. Response Caching

**Configuration:**
- Added `ResponseCaching` middleware in `Program.cs`
- Applied `[ResponseCache]` attribute to `/me` endpoint
- Cache duration: 5 minutes
- Varies by: Cookie (per-user caching)

### 6. CORS Configuration

**Settings:**
- **Allowed Origins**: `http://localhost:5173` (Vite), `http://localhost:3000` (React dev)
- **Allows**: Any header, any method
- **Credentials**: Enabled (required for cookie authentication)
- **Policy Name**: "AllowFrontend"

## Frontend Implementation

### 1. Project Setup

**Technology Stack:**
- **Framework**: React 18
- **Language**: TypeScript
- **Build Tool**: Vite 7
- **Router**: React Router DOM
- **Location**: `/frontend/portal-empleado/`

### 2. Project Structure

```
frontend/portal-empleado/
├── src/
│   ├── pages/
│   │   ├── Login.tsx/css          # Google OAuth login page
│   │   ├── Home.tsx/css           # Main dashboard
│   │   ├── Recibos.tsx/css        # Payroll receipts (placeholder)
│   │   └── Vacaciones.tsx/css     # Vacation requests (placeholder)
│   ├── services/
│   │   └── auth.ts                # Authentication API service
│   ├── types/
│   │   └── index.ts               # TypeScript type definitions
│   ├── App.tsx                    # Main app with routing
│   ├── main.tsx                   # Entry point
│   └── index.css                  # Global styles
├── package.json
├── tsconfig.json
└── vite.config.ts
```

### 3. Page Descriptions

#### Login Page (`/login`)
- Displays "Login with Google" button
- Redirects to backend `/api/auth/login` endpoint
- Clean, centered design with gradient background

#### Home Page (`/`)
- Calls `GET /api/auth/me` on mount
- Redirects to `/login` if 401 (unauthorized)
- Displays user information (name, email, profile picture)
- Navigation to Recibos and Vacaciones
- Logout button with CSRF protection

#### Recibos Page (`/recibos`)
- Placeholder for payroll receipts functionality
- Indicates integration with Nomina microservice
- Navigation back to Home

#### Vacaciones Page (`/vacaciones`)
- Placeholder for vacation requests functionality
- Indicates integration with Asistencia microservice
- Navigation back to Home

### 4. Authentication Service

**Functions:**
- `getCurrentUser()` - Fetches current user from `/api/auth/me`
  - Includes credentials (cookies)
  - Returns `null` on 401
  - Throws error on other failures

- `logout()` - Logs out user via `/api/auth/logout`
  - Fetches CSRF token first
  - Includes token in logout request
  - Includes credentials (cookies)

- `getLoginUrl()` - Returns backend login URL

### 5. Type Definitions

```typescript
interface User {
  id: string;
  email: string;
  fullName: string;
  pictureUrl?: string;
  createdAt: string;
}

interface ApiError {
  message: string;
  status: number;
}
```

### 6. Routing Configuration

- `/login` - Login page (public)
- `/` - Home page (protected)
- `/recibos` - Recibos page (protected)
- `/vacaciones` - Vacaciones page (protected)
- `*` - Redirects to Home

### 7. Styling

- Responsive design
- Modern UI with gradient accents
- Consistent color scheme (purple/blue gradients)
- Card-based layouts
- Smooth transitions and hover effects

## Architecture Benefits

### 1. Clean Architecture
- **Separation of Concerns**: Each layer has clear responsibilities
- **Dependency Rule**: Dependencies point inward (Domain ← Application ← Infrastructure ← API)
- **Testability**: Easy to unit test handlers independently
- **Maintainability**: Changes in one layer don't affect others

### 2. CQRS Pattern
- **Command/Query Separation**: Clear distinction between reads and writes
- **Single Responsibility**: Each handler does one thing
- **Pipeline Behavior**: Easy to add cross-cutting concerns (logging, validation, etc.)

### 3. Repository Pattern
- **Data Access Abstraction**: Controllers don't know about DbContext
- **Testability**: Easy to mock data access
- **Flexibility**: Can swap implementations (e.g., switch to different ORM)

### 4. BFF Architecture
- **Frontend-Optimized**: API designed specifically for frontend needs
- **Response Caching**: Improves performance for frequently accessed data
- **CORS Configuration**: Secure cross-origin requests
- **CSRF Protection**: Secure state-changing operations

## Security Considerations

### 1. Authentication
- Cookie-based authentication
- Secure cookie settings (HttpOnly, Secure, SameSite)
- 401 handling on frontend with redirect to login

### 2. CSRF Protection
- Anti-forgery tokens for state-changing operations
- Token obtained via separate endpoint
- Validated on logout and data modification endpoints

### 3. CORS
- Restricted origins (localhost only for development)
- Credentials allowed (for cookies)
- Prepared for production configuration

## Future Enhancements

### 1. Microservice Integration
- Implement `INominaService` for payroll data
- Implement `IAsistenciaService` for attendance/vacation data
- Use HTTP clients (e.g., `HttpClient` with Polly for resilience)
- Consider message queues for async operations

### 2. Additional Features
- Add validation with FluentValidation
- Implement AutoMapper for DTO mapping
- Add global error handling in frontend
- Implement loading states and error boundaries
- Add unit and integration tests
- Implement API versioning
- Add health checks

### 3. Production Readiness
- Environment-based configuration
- Proper logging and monitoring
- Performance optimization
- Security hardening (rate limiting already in place)
- CI/CD pipeline integration

## Running the Application

### Backend
```bash
cd SAED-PortalEmpleado.Api
dotnet run
```
Runs on: https://localhost:7079

### Frontend
```bash
cd frontend/portal-empleado
npm install
npm run dev
```
Runs on: http://localhost:5173

## Build Commands

### Backend
```bash
dotnet build
```

### Frontend
```bash
cd frontend/portal-empleado
npm run build
```

## Notes

- Frontend development server proxies API requests to avoid CORS issues during development
- Backend requires SQL Server and proper configuration in `appsettings.json`
- Google OAuth credentials must be configured in `appsettings.json` or user secrets
- CSRF tokens are required for POST/PUT/DELETE operations
- Response caching is enabled for performance optimization
