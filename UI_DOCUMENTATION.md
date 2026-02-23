# Portal Empleado - UI Documentation

This document describes the Razor Pages UI that has been added to the Portal Empleado application.

## Overview

The application now includes a web-based user interface built with ASP.NET Core Razor Pages and Bootstrap 5.3.0. The UI provides a simple, intuitive interface for employees to access their payroll receipts and vacation information.

## Features

### Home Page (/)

**Before Login:**
- Displays the application title and description
- Shows a "Login with Google" button
- Uses Bootstrap styling for a clean, professional look

**After Login:**
- Displays a welcome message with the employee's name
- Shows the employee's profile picture (if available from Google)
- Displays the employee's email address
- Provides quick access buttons to:
  - Recibos (Payroll Receipts)
  - Vacaciones (Vacation Management)

### Navigation Bar

**When Authenticated:**
- Application logo/name (links to home)
- Navigation links to Recibos and Vacaciones
- User dropdown menu with:
  - Employee name display
  - Logout option

### Protected Pages

Both the following pages are protected with the `[Authorize]` attribute and require authentication:

#### Recibos (/recibos)
- Displays payroll receipts information
- Shows employee name
- Placeholder content for future implementation
- Back button to return to home page

#### Vacaciones (/vacaciones)
- Displays vacation management information
- Shows available and used vacation days
- Displays recent vacation requests
- Back button to return to home page

### Logout (/logout)

- POST-only endpoint that signs the user out
- Redirects to the home page after logout
- Accessible from the navigation bar dropdown menu

## Technical Implementation

### Architecture

The UI follows the existing Clean Architecture pattern of the solution:
- **Pages folder**: Contains all Razor Pages
- **Shared Layout**: `Pages/Shared/_Layout.cshtml`
- **View configuration**: `_ViewImports.cshtml` and `_ViewStart.cshtml`

### Authentication Flow

1. User accesses the home page (/)
2. User clicks "Login with Google"
3. Application redirects to Google OAuth consent screen
4. User authenticates with Google
5. Google redirects back to `/api/auth/google-callback`
6. Application creates/updates employee record in database
7. Application establishes authenticated session with cookie
8. User is redirected to home page (now showing authenticated state)

### Authorization

- **Public Pages**: Home page (/)
- **Protected Pages**: Recibos, Vacaciones (require `[Authorize]` attribute)
- **Unauthenticated Access**: Attempting to access protected pages redirects to Google login

### Technology Stack

- **ASP.NET Core 8.0**: Web framework
- **Razor Pages**: Page-based programming model
- **Bootstrap 5.3.0**: UI framework (loaded from CDN)
- **Google OAuth 2.0**: Authentication provider
- **Cookie Authentication**: Session management

## Configuration

### Required Settings

Update `appsettings.json` or use User Secrets to configure Google OAuth:

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

### Using User Secrets (Recommended for Development)

```bash
cd SAED-PortalEmpleado.Api
dotnet user-secrets set "Authentication:Google:ClientId" "your-client-id"
dotnet user-secrets set "Authentication:Google:ClientSecret" "your-client-secret"
```

### Google Cloud Console Configuration

1. Go to [Google Cloud Console](https://console.cloud.google.com/)
2. Create or select a project
3. Enable Google+ API
4. Create OAuth 2.0 credentials (Web application)
5. Add authorized redirect URI:
   - Development: `https://localhost:7079/signin-google`
   - Production: `https://yourdomain.com/signin-google`

## Running the Application

1. **Configure Google OAuth credentials** (see Configuration section above)

2. **Build the solution:**
   ```bash
   dotnet build
   ```

3. **Update database** (if not already done):
   ```bash
   cd SAED-PortalEmpleado.Api
   dotnet ef database update
   ```

4. **Run the application:**
   ```bash
   cd SAED-PortalEmpleado.Api
   dotnet run
   ```

5. **Access the application:**
   - HTTP: http://localhost:5011
   - HTTPS: https://localhost:7079 (recommended)
   - The home page will load automatically

## User Experience Flow

### First Time User

1. Navigate to application home page
2. Click "Iniciar Sesión con Google" button
3. Authenticate with Google account
4. Grant permissions to the application
5. Automatically redirected to home page (now authenticated)
6. See welcome message with name and profile picture
7. Navigate to Recibos or Vacaciones as needed

### Returning User

1. Navigate to application home page
2. If session is still active (within 24 hours), see authenticated home page
3. If session expired, click "Iniciar Sesión con Google" to re-authenticate

### Logging Out

1. Click on your name in the navigation bar
2. Click "Cerrar Sesión" (Logout)
3. Redirected to home page (unauthenticated state)

## Customization

### Styling

The UI uses Bootstrap 5.3.0. To customize:
- Modify `Pages/Shared/_Layout.cshtml` to change layout
- Add custom CSS by including a stylesheet in the layout
- Modify individual page `.cshtml` files for page-specific changes

### Adding New Pages

1. Create new `.cshtml` and `.cshtml.cs` files in the `Pages` folder
2. Add `[Authorize]` attribute to the PageModel class if authentication is required
3. Add navigation links in `_Layout.cshtml` if needed

### Internationalization

Currently, the UI is in Spanish. To support multiple languages:
- Implement ASP.NET Core localization
- Create resource files for different languages
- Update razor pages to use localized strings

## Security Considerations

- ✅ HTTPS enforced via `UseHttpsRedirection()`
- ✅ Google OAuth 2.0 for authentication
- ✅ Secure cookie-based session management
- ✅ Protected pages require authentication via `[Authorize]` attribute
- ✅ CSRF protection enabled by default for POST requests
- ✅ Credentials should never be committed to source control

## Future Enhancements

The current implementation provides a foundation for:
- Implementing actual payroll receipt display and download
- Adding vacation request and approval workflows
- Creating an employee profile page
- Adding notifications and alerts
- Implementing role-based authorization (admin vs employee)
- Adding search and filtering capabilities
- Implementing pagination for large datasets

## Troubleshooting

### "Google ClientId not configured" error
- Verify Google OAuth credentials are set in `appsettings.json` or User Secrets

### Pages not loading
- Ensure `MapRazorPages()` is called in `Program.cs`
- Check that the application is running and accessible

### Bootstrap not styling correctly
- Check browser console for CDN loading errors
- Verify internet connection for CDN access
- Consider downloading Bootstrap locally if CDN access is blocked

### Redirect loop or authentication issues
- Clear browser cookies
- Verify Google OAuth redirect URI matches configured URI
- Check that cookie authentication is properly configured

## Support

For issues or questions:
- Check the main [README.md](README.md) for general information
- Review [AUTHENTICATION.md](AUTHENTICATION.md) for authentication details
- Review application logs in the console when running in Development mode
