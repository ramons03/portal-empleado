# SaedSecurityPoC - Portal Empleado

A .NET 8 security proof of concept with three interconnected projects demonstrating Google OIDC authentication, domain policy validation, JWT token generation, and protected API access.

## Projects

### 1. Saed.Auth (MVC Razor + Google OIDC)
- Handles Google OIDC authentication
- Implements domain policy validation (Strict/Edu/Public)
- Validates email domains based on configurable policies
- Issues internal JWT tokens (issuer=saed-auth, audience=saed-api)
- **Does NOT expose Google tokens** - only internal JWT

### 2. Saed.Api (Web API)
- JWT Bearer authentication
- Protected endpoint at `/api/secure`
- Validates JWT tokens from Saed.Auth
- Returns user information from JWT claims

### 3. Saed.Front (React)
- Login buttons for each domain policy
- Stores JWT in memory (not in localStorage or cookies)
- Calls protected API with JWT token
- Displays secure data from API

## Configuration

### Google OAuth Setup

1. Go to [Google Cloud Console](https://console.cloud.google.com/)
2. Create a new project or select an existing one
3. Enable Google+ API
4. Create OAuth 2.0 credentials
5. Add authorized redirect URIs:
   - `https://localhost:5001/signin-google`
6. Copy the Client ID and Client Secret

### Update appsettings.json

#### Saed.Auth/appsettings.json
```json
{
  "GoogleAuth": {
    "ClientId": "YOUR_GOOGLE_CLIENT_ID",
    "ClientSecret": "YOUR_GOOGLE_CLIENT_SECRET"
  },
  "DomainPolicies": {
    "Strict": {
      "AllowedDomains": ["example.com", "yourcompany.com"]
    },
    "Edu": {
      "AllowedDomains": ["*.edu"]
    },
    "Public": {
      "AllowedDomains": ["*"]
    }
  },
  "JwtSettings": {
    "SecretKey": "YOUR_SECRET_KEY_MINIMUM_32_CHARACTERS_LONG",
    "Issuer": "saed-auth",
    "Audience": "saed-api",
    "ExpiryMinutes": 60
  }
}
```

#### Saed.Api/appsettings.json
```json
{
  "JwtSettings": {
    "SecretKey": "YOUR_SECRET_KEY_MINIMUM_32_CHARACTERS_LONG",
    "Issuer": "saed-auth",
    "Audience": "saed-api",
    "ExpiryMinutes": 60
  }
}
```

**Important:** Use the same `SecretKey` in both projects!

## Running the Solution

### 1. Start Saed.Auth (Port 5001)
```bash
cd Saed.Auth
dotnet run
```
Navigate to: https://localhost:5001

### 2. Start Saed.Api (Port 5002)
```bash
cd Saed.Api
dotnet run
```
Navigate to: https://localhost:5002/swagger

### 3. Start Saed.Front (Port 3000)
```bash
cd Saed.Front/client
npm start
```
Navigate to: http://localhost:3000

## Usage Flow

1. **Login via React App:**
   - Open http://localhost:3000
   - Click one of the login buttons (Strict/Edu/Public policy)
   - Authenticate with Google in the new tab
   - Copy the JWT token displayed

2. **Use JWT Token:**
   - Paste the JWT token in the React app
   - Click "Store Token in Memory"
   - Click "Call /api/secure"

3. **View Results:**
   - The React app displays the secure data from the API
   - API validates the JWT and returns user information

## Domain Policies

- **Strict:** Only allows specific domains configured in appsettings.json
- **Edu:** Only allows educational institution domains (*.edu)
- **Public:** Allows all domains (no restrictions)

## Security Features

- Google tokens are never exposed to the client
- Only internal JWT tokens are issued and used
- JWT tokens are stored in memory (not localStorage)
- Domain validation enforced based on policy
- JWT signature validation on API
- CORS configured for security

## API Endpoints

### Saed.Auth
- `GET /Auth/Login` - Login page with policy selection
- `GET /Auth/GoogleLogin?policy={Strict|Edu|Public}` - Initiates Google login
- `GET /Auth/GoogleResponse?policy={policy}` - Handles Google callback
- `GET /Auth/Logout` - Logout

### Saed.Api
- `GET /api/secure` - Protected endpoint (requires JWT)
- `GET /api/public` - Public endpoint (no authentication)

## Technology Stack

- **.NET 8.0**
- **ASP.NET Core MVC**
- **ASP.NET Core Web API**
- **React 18 with TypeScript**
- **Google OIDC Authentication**
- **JWT Bearer Authentication**
- **Axios for HTTP requests**

## Development

### Build All Projects
```bash
dotnet build SaedSecurityPoC.sln
```

### Run Tests (if added)
```bash
dotnet test
```

## Notes

- This is a proof of concept for demonstration purposes
- Update all security keys and secrets before production use
- Configure proper CORS policies for production
- Use HTTPS in production
- Consider using a proper secret management solution (Azure Key Vault, etc.)
