# Google OpenID Connect Authentication Setup

This document explains how to configure and use Google OpenID Connect authentication in the SAED Portal Empleado application.

## Prerequisites

1. A Google Cloud Console project with OAuth 2.0 credentials
2. Authorized redirect URIs configured in Google Cloud Console

## Configuration Steps

### 1. Set up Google OAuth 2.0 Credentials

1. Go to [Google Cloud Console](https://console.cloud.google.com/)
2. Create a new project or select an existing one
3. Navigate to "APIs & Services" > "Credentials"
4. Click "Create Credentials" > "OAuth 2.0 Client ID"
5. Choose "Web application"
6. Add authorized redirect URIs:
   - Development: `https://localhost:7079/signin-google`
   - Production: `https://yourdomain.com/signin-google`
7. Save the Client ID and Client Secret

### 2. Configure appsettings.json

Update the `appsettings.json` file with your Google OAuth credentials:

```json
{
  "Authentication": {
    "Google": {
      "ClientId": "your-client-id.apps.googleusercontent.com",
      "ClientSecret": "your-client-secret"
    }
  }
}
```

**Important**: For production, use User Secrets or Environment Variables instead of hardcoding credentials:

```bash
# Using User Secrets (Development)
dotnet user-secrets set "Authentication:Google:ClientId" "your-client-id"
dotnet user-secrets set "Authentication:Google:ClientSecret" "your-client-secret"

# Using Environment Variables (Production)
export Authentication__Google__ClientId="your-client-id"
export Authentication__Google__ClientSecret="your-client-secret"
```

### 3. Authentication Flow

The authentication implementation includes the following endpoints:

#### Public Endpoints (No Authentication Required)

- `GET /api/auth/login` - Initiates the Google OAuth login flow
- `GET /api/auth/google-callback` - Handles the OAuth callback from Google

#### Protected Endpoints (Authentication Required)

- `POST /api/auth/logout` - Logs out the current user
- `GET /api/auth/me` - Returns current authenticated user information
- `GET /api/employees` - Get all employees
- `GET /api/employees/{id}` - Get employee by ID
- `POST /api/employees` - Create a new employee
- `PUT /api/employees/{id}` - Update an employee
- `DELETE /api/employees/{id}` - Delete an employee

### 4. How Authentication Works

1. **Login Flow**:
   - User visits `/api/auth/login`
   - Application redirects to Google's OAuth consent screen
   - User authenticates with Google and grants permissions
   - Google redirects back to `/api/auth/google-callback`
   - Application extracts user claims (sub, email, name, picture)
   - Employee record is created or updated in the database
   - User session is established with a cookie

2. **Authorization**:
   - All API endpoints (except auth/login and auth/google-callback) require authentication
   - Protected endpoints use the `RequireAuthenticatedUser` authorization policy
   - Unauthenticated requests return 401 Unauthorized

3. **Claims Extracted from Google**:
   - `sub` - Google's unique identifier for the user (stored as GoogleSub)
   - `email` - User's email address
   - `name` - User's full name
   - `picture` - URL to user's profile picture

## Testing the Authentication

### Manual Testing

1. Start the application:
   ```bash
   cd SAED-PortalEmpleado.Api
   dotnet run
   ```

2. Navigate to `https://localhost:7079/swagger`

3. Try to access a protected endpoint (e.g., `GET /api/employees`) - should return 401

4. Navigate to `https://localhost:7079/api/auth/login` in a browser

5. Complete the Google authentication flow

6. After successful authentication, you should be redirected to the callback endpoint

7. Now access `GET /api/auth/me` to see your user information

8. Try protected endpoints again - they should now work

## Security Considerations

1. **HTTPS Required**: The application uses `app.UseHttpsRedirection()` to enforce HTTPS
2. **Secure Cookies**: Cookie authentication is configured with secure defaults
3. **Token Storage**: Google tokens are saved with `SaveTokens = true` for future API calls
4. **Authorization Policy**: The `RequireAuthenticatedUser` policy ensures all protected endpoints require authentication
5. **Credential Management**: Never commit credentials to source control - use User Secrets or Environment Variables

## Architecture

The authentication implementation follows Clean Architecture principles:

- **Program.cs**: Configures authentication middleware and services
- **AuthController**: Handles authentication flows and user persistence
- **EmployeesController**: Protected with `[Authorize]` attribute
- **ApplicationDbContext**: Stores employee records with Google authentication data

## Database Schema

The `Employee` entity stores the following authentication-related fields:

- `GoogleSub` (string, required, unique) - Google's unique identifier
- `Email` (string, required, unique) - User's email
- `FullName` (string, required) - User's full name
- `PictureUrl` (string, optional) - URL to profile picture
- `CreatedAt` (DateTime, required) - Account creation timestamp

## Troubleshooting

### "Google ClientId not configured" error
- Verify that `Authentication:Google:ClientId` is set in appsettings.json or environment variables

### "Google ClientSecret not configured" error
- Verify that `Authentication:Google:ClientSecret` is set in appsettings.json or environment variables

### Redirect URI mismatch
- Ensure the redirect URI in Google Cloud Console matches: `https://yourdomain.com/signin-google`
- For local development: `https://localhost:7079/signin-google`

### 401 Unauthorized on protected endpoints
- Ensure you've completed the login flow at `/api/auth/login`
- Check that cookies are enabled in your browser
- Verify the session hasn't expired (default: 24 hours)

## Additional Resources

- [Google OAuth 2.0 Documentation](https://developers.google.com/identity/protocols/oauth2)
- [ASP.NET Core Authentication](https://learn.microsoft.com/en-us/aspnet/core/security/authentication/)
- [Google Authentication in ASP.NET Core](https://learn.microsoft.com/en-us/aspnet/core/security/authentication/social/google-logins)
