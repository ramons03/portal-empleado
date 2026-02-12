# Architecture Overview - SaedSecurityPoC

## System Architecture

```
┌─────────────────────────────────────────────────────────────────────┐
│                        User (Web Browser)                           │
└──────────────────────────┬──────────────────────────────────────────┘
                           │
                           │
         ┌─────────────────┼─────────────────┐
         │                 │                 │
         │                 │                 │
         ▼                 ▼                 ▼
┌────────────────┐  ┌─────────────┐  ┌──────────────┐
│   Saed.Front   │  │ Saed.Auth   │  │  Saed.Api    │
│   (React)      │  │ (MVC Razor) │  │  (Web API)   │
│  Port: 3000    │  │ Port: 5001  │  │  Port: 5002  │
└────────────────┘  └─────────────┘  └──────────────┘
         │                 │                 │
         │                 │                 │
         │         ┌───────┴────────┐        │
         │         │  Google OAuth   │        │
         │         │     (OIDC)      │        │
         │         └────────────────┘        │
         │                                    │
         └──────── JWT Token ─────────────────┘
```

## Component Descriptions

### 1. Saed.Front (React Application)
- **Technology:** React 18 with TypeScript
- **Port:** 3000
- **Purpose:** Client-facing web application
- **Key Features:**
  - Login buttons for each domain policy (Strict/Edu/Public)
  - JWT token storage in memory (not localStorage)
  - Protected API calls with Bearer authentication
  - User-friendly interface for testing authentication flow

### 2. Saed.Auth (MVC Razor Application)
- **Technology:** ASP.NET Core 8.0 MVC
- **Port:** 5001 (HTTPS)
- **Purpose:** Authentication service and JWT token issuer
- **Key Features:**
  - Google OIDC authentication integration
  - Domain policy validation (Strict/Edu/Public)
  - Email domain validation
  - Internal JWT token generation
  - Google tokens never exposed to client
  - Configurable domain policies via appsettings.json

### 3. Saed.Api (Web API)
- **Technology:** ASP.NET Core 8.0 Web API
- **Port:** 5002 (HTTPS)
- **Purpose:** Protected API service
- **Key Features:**
  - JWT Bearer authentication
  - Protected endpoints requiring valid JWT
  - Public endpoints without authentication
  - JWT validation (issuer, audience, signature)
  - Swagger/OpenAPI documentation

## Authentication Flow

### Step-by-Step Flow

```
1. User clicks login button in React app (selects policy)
   │
   ├─▶ Browser redirects to Saed.Auth
   │
2. Saed.Auth initiates Google OIDC flow
   │
   ├─▶ User redirects to Google
   │
3. User authenticates with Google
   │
   ├─▶ Google returns to Saed.Auth with user info
   │
4. Saed.Auth validates email against policy
   │
   ├─▶ If valid: Generate internal JWT
   │   • Issuer: saed-auth
   │   • Audience: saed-api
   │   • Claims: email, name, policy
   │   • Signature: HMAC-SHA256
   │
   ├─▶ Display JWT to user (Google tokens discarded)
   │
5. User copies JWT and pastes in React app
   │
   ├─▶ JWT stored in React component state (memory)
   │
6. React app calls Saed.Api with JWT
   │
   ├─▶ Authorization: Bearer <JWT>
   │
7. Saed.Api validates JWT
   │
   ├─▶ Checks: signature, issuer, audience, expiry
   │
   ├─▶ If valid: Return protected data
   └─▶ If invalid: Return 401 Unauthorized
```

## Domain Policies

### Strict Policy
- Only allows specific domains configured in appsettings.json
- Example: `["yourdomain.com", "example.com"]`
- Use case: Internal corporate applications

### Edu Policy
- Only allows educational institution domains (*.edu)
- Pattern matching: `*.edu`
- Use case: Academic applications

### Public Policy
- Allows all email domains
- Wildcard: `["*"]`
- Use case: Public-facing applications

## Security Features

### 1. No Google Token Exposure
- Google OAuth tokens are handled server-side only
- Client receives only internal JWT tokens
- Google tokens are not saved (`SaveTokens = false`)

### 2. JWT Token Security
- Signed with HMAC-SHA256
- Contains minimal claims (email, name, policy)
- Short expiration time (configurable, default 60 minutes)
- Validated on every API request

### 3. Domain Validation
- Email domains validated before token issuance
- Configurable policies via appsettings
- Failed validation prevents token generation

### 4. CORS Configuration
- Restricted to specific origins
- Configured for development (localhost)
- Should be updated for production

### 5. In-Memory Token Storage
- JWT stored in React component state
- Not persisted in localStorage or cookies
- Lost on page refresh (by design for demo)

## Data Flow

### Authentication Data Flow

```
User Email & Password
         ↓
    Google OAuth
         ↓
  [Saed.Auth validates]
         ↓
  Internal JWT Token
  {
    "email": "user@example.com",
    "name": "John Doe",
    "policy": "Strict",
    "iss": "saed-auth",
    "aud": "saed-api",
    "exp": <timestamp>
  }
         ↓
    React App (Memory)
         ↓
  API Request with JWT
         ↓
  [Saed.Api validates JWT]
         ↓
    Protected Data
```

## Configuration

### Shared Configuration

Both Saed.Auth and Saed.Api must share:
- **SecretKey:** Same key for signing and validation
- **Issuer:** "saed-auth"
- **Audience:** "saed-api"

### Environment-Specific Configuration

Development:
- Use appsettings.json for configuration
- Trust development certificates
- CORS allows localhost origins

Production:
- Use Azure Key Vault or similar
- Proper SSL certificates
- CORS configured for production domains
- Secrets in environment variables

## API Endpoints

### Saed.Auth Endpoints

| Endpoint | Method | Description | Auth Required |
|----------|--------|-------------|---------------|
| `/Auth/Login` | GET | Login page with policy selection | No |
| `/Auth/GoogleLogin?policy={policy}` | GET | Initiates Google OAuth flow | No |
| `/Auth/GoogleResponse?policy={policy}` | GET | Handles Google callback | No |
| `/Auth/Logout` | GET | Logout and clear session | No |

### Saed.Api Endpoints

| Endpoint | Method | Description | Auth Required |
|----------|--------|-------------|---------------|
| `/api/secure` | GET | Returns protected data | Yes (JWT) |
| `/api/public` | GET | Returns public data | No |

## Technology Stack

### Backend (.NET)
- ASP.NET Core 8.0
- Microsoft.AspNetCore.Authentication.Google
- Microsoft.AspNetCore.Authentication.JwtBearer
- System.IdentityModel.Tokens.Jwt
- Entity Framework Core (if database needed)

### Frontend (React)
- React 18
- TypeScript
- Axios (HTTP client)
- Create React App

### Authentication
- Google OAuth 2.0 / OpenID Connect
- JWT (JSON Web Tokens)
- HMAC-SHA256 signature

## Deployment Considerations

### Development
- Run locally with `dotnet run` and `npm start`
- Use development certificates
- Configure Google OAuth redirect URI for localhost

### Production
- Deploy to Azure App Service, AWS, or similar
- Use managed SSL certificates
- Configure production redirect URIs
- Use Azure Key Vault for secrets
- Enable Application Insights or logging
- Configure proper CORS
- Use CDN for React app
- Enable rate limiting
- Implement refresh tokens

## Future Enhancements

1. **Refresh Tokens:** Implement refresh token flow for longer sessions
2. **Database:** Add user management and audit logging
3. **Role-Based Access Control:** Add roles and permissions
4. **Multi-Factor Authentication:** Add MFA support
5. **Social Logins:** Add more OAuth providers (Microsoft, GitHub, etc.)
6. **API Rate Limiting:** Protect API from abuse
7. **Logging & Monitoring:** Add comprehensive logging
8. **Unit Tests:** Add test coverage for all components
9. **CI/CD Pipeline:** Automate build and deployment
10. **Docker Support:** Containerize applications

## Testing

### Manual Testing
1. Test each domain policy (Strict, Edu, Public)
2. Test with valid and invalid email domains
3. Test API access with valid JWT
4. Test API access with expired JWT
5. Test API access without JWT

### Automated Testing (Future)
- Unit tests for services
- Integration tests for API
- E2E tests for React app
- Security testing
