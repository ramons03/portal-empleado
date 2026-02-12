# Setup Guide for SaedSecurityPoC

This guide will walk you through setting up and running the SaedSecurityPoC solution.

## Prerequisites

- .NET 8.0 SDK or later
- Node.js 16+ and npm
- A Google Cloud account (for OAuth setup)
- A code editor (Visual Studio, VS Code, etc.)

## Step 1: Google OAuth Setup

1. **Go to Google Cloud Console:**
   - Navigate to https://console.cloud.google.com/
   - Create a new project or select an existing one

2. **Enable Google+ API:**
   - In the left menu, go to "APIs & Services" > "Library"
   - Search for "Google+ API"
   - Click "Enable"

3. **Create OAuth 2.0 Credentials:**
   - Go to "APIs & Services" > "Credentials"
   - Click "Create Credentials" > "OAuth client ID"
   - Select "Web application"
   - Add the following authorized redirect URI:
     ```
     https://localhost:5001/signin-google
     ```
   - Click "Create"
   - Copy the **Client ID** and **Client Secret**

## Step 2: Configure the Applications

### Generate a Secret Key

First, generate a secure secret key (at least 32 characters) for JWT signing:

```bash
# You can use this PowerShell command:
-join ((48..57) + (65..90) + (97..122) | Get-Random -Count 32 | % {[char]$_})

# Or this bash command:
openssl rand -base64 32
```

### Configure Saed.Auth

Edit `Saed.Auth/appsettings.json`:

```json
{
  "GoogleAuth": {
    "ClientId": "YOUR_GOOGLE_CLIENT_ID_HERE",
    "ClientSecret": "YOUR_GOOGLE_CLIENT_SECRET_HERE"
  },
  "DomainPolicies": {
    "Strict": {
      "AllowedDomains": ["yourdomain.com", "example.com"]
    },
    "Edu": {
      "AllowedDomains": ["*.edu"]
    },
    "Public": {
      "AllowedDomains": ["*"]
    }
  },
  "JwtSettings": {
    "SecretKey": "YOUR_GENERATED_SECRET_KEY_HERE",
    "Issuer": "saed-auth",
    "Audience": "saed-api",
    "ExpiryMinutes": 60
  }
}
```

**Important:** 
- Replace `YOUR_GOOGLE_CLIENT_ID_HERE` with your Google Client ID
- Replace `YOUR_GOOGLE_CLIENT_SECRET_HERE` with your Google Client Secret
- Replace `YOUR_GENERATED_SECRET_KEY_HERE` with your generated secret key
- Update the "Strict" policy domains to match your requirements

### Configure Saed.Api

Edit `Saed.Api/appsettings.json`:

```json
{
  "JwtSettings": {
    "SecretKey": "YOUR_GENERATED_SECRET_KEY_HERE",
    "Issuer": "saed-auth",
    "Audience": "saed-api",
    "ExpiryMinutes": 60
  }
}
```

**Important:** 
- Use the **SAME** `SecretKey` as in Saed.Auth

## Step 3: Install Dependencies

### .NET Projects

```bash
# Restore NuGet packages
dotnet restore SaedSecurityPoC.sln
```

### React Frontend

```bash
# Navigate to React app
cd Saed.Front/client

# Install dependencies
npm install
```

## Step 4: Build the Solution

```bash
# Build .NET projects
dotnet build SaedSecurityPoC.sln

# Build React app
cd Saed.Front/client
npm run build
cd ../..
```

## Step 5: Run the Applications

You'll need three terminal windows to run all three applications.

### Terminal 1: Start Saed.Auth

```bash
cd Saed.Auth
dotnet run
```

The application will start on **https://localhost:5001**

### Terminal 2: Start Saed.Api

```bash
cd Saed.Api
dotnet run
```

The API will start on **https://localhost:5002**

You can access Swagger at: https://localhost:5002/swagger

### Terminal 3: Start Saed.Front

```bash
cd Saed.Front/client
npm start
```

The React app will start on **http://localhost:3000**

## Step 6: Test the Application

1. **Open the React app:**
   - Navigate to http://localhost:3000

2. **Login with a policy:**
   - Click one of the three login buttons (Strict, Edu, or Public)
   - A new tab will open with the Auth service
   - Click "Login with Google"
   - Authenticate with your Google account
   - After successful authentication, you'll see a page with your JWT token

3. **Copy the JWT token:**
   - Select and copy the entire JWT token from the textarea

4. **Use the token in React app:**
   - Return to the React app at http://localhost:3000
   - Paste the JWT token in the textarea
   - Click "Store Token in Memory"

5. **Call the protected API:**
   - Click "Call /api/secure"
   - You should see the secure data returned from the API

## Troubleshooting

### HTTPS Certificate Issues

If you encounter SSL/TLS certificate errors:

```bash
# Trust the development certificates
dotnet dev-certs https --trust
```

### CORS Issues

Make sure all three applications are running on the correct ports:
- Saed.Auth: https://localhost:5001
- Saed.Api: https://localhost:5002
- Saed.Front: http://localhost:3000

### Google Authentication Issues

- Verify your redirect URI is exactly: `https://localhost:5001/signin-google`
- Make sure the Client ID and Client Secret are correctly configured
- Ensure Google+ API is enabled in your Google Cloud project

### Domain Policy Failures

If you get "Email domain not allowed" errors:
- Check that your email domain matches the policy you selected
- Update the `DomainPolicies` in `Saed.Auth/appsettings.json` to include your domain
- Restart the Saed.Auth application after changing settings

## Security Notes

- **Never commit secrets to source control**
- Use environment variables or Azure Key Vault for production
- The provided secret keys are placeholders - generate your own
- Enable proper logging and monitoring in production
- Configure proper CORS policies for your production domains
- Use HTTPS in production
- Implement proper error handling and logging
- Consider implementing refresh tokens for production use

## Next Steps

- Implement refresh tokens
- Add proper error logging
- Add unit and integration tests
- Configure for production deployment
- Implement proper secret management
- Add rate limiting
- Implement additional security headers
