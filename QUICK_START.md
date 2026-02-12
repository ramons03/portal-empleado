# Quick Start Configuration Checklist

Before running the SaedSecurityPoC solution, you need to configure a few files. Follow this checklist:

## ☐ Step 1: Get Google OAuth Credentials

1. Go to https://console.cloud.google.com/
2. Create a new project
3. Enable Google+ API
4. Create OAuth 2.0 Client ID
5. Add redirect URI: `https://localhost:5001/signin-google`
6. Save your Client ID and Client Secret

## ☐ Step 2: Generate Secret Key

Generate a secure secret key (minimum 32 characters):

**PowerShell:**
```powershell
-join ((48..57) + (65..90) + (97..122) | Get-Random -Count 32 | % {[char]$_})
```

**Bash/Linux:**
```bash
openssl rand -base64 32
```

**Or use any secure random string generator.**

## ☐ Step 3: Configure Saed.Auth

Edit: `Saed.Auth/appsettings.json`

Replace these placeholders:
- `YOUR_GOOGLE_CLIENT_ID` → Your Google Client ID
- `YOUR_GOOGLE_CLIENT_SECRET` → Your Google Client Secret
- `YOUR_SECRET_KEY_MINIMUM_32_CHARACTERS_LONG_FOR_HS256` → Your generated secret key

Optional: Update domain policies under `DomainPolicies > Strict > AllowedDomains` to include your domains.

## ☐ Step 4: Configure Saed.Api

Edit: `Saed.Api/appsettings.json`

Replace this placeholder:
- `YOUR_SECRET_KEY_MINIMUM_32_CHARACTERS_LONG_FOR_HS256` → **Same secret key as Step 3**

⚠️ **Important:** The secret key must be EXACTLY the same in both files!

## ☐ Step 5: Trust Development Certificates

```bash
dotnet dev-certs https --trust
```

Click "Yes" when prompted.

## ☐ Step 6: Install Dependencies

```bash
# .NET projects (run from solution root)
dotnet restore

# React app
cd Saed.Front/client
npm install
cd ../..
```

## ☐ Step 7: Build Everything

```bash
# Build .NET projects
dotnet build SaedSecurityPoC.sln

# Build React app
cd Saed.Front/client
npm run build
cd ../..
```

## ☐ Step 8: Run Applications

Open 3 terminal windows:

**Terminal 1 - Saed.Auth:**
```bash
cd Saed.Auth
dotnet run
```
Should start on https://localhost:5001

**Terminal 2 - Saed.Api:**
```bash
cd Saed.Api
dotnet run
```
Should start on https://localhost:5002

**Terminal 3 - Saed.Front:**
```bash
cd Saed.Front/client
npm start
```
Should start on http://localhost:3000

## ☐ Step 9: Test the Application

1. Open http://localhost:3000
2. Click "Login with Public Policy"
3. Authenticate with Google
4. Copy the JWT token
5. Return to React app
6. Paste token and click "Store Token in Memory"
7. Click "Call /api/secure"
8. You should see secure data from the API!

## ✅ Configuration Files Summary

| File | What to Change |
|------|----------------|
| `Saed.Auth/appsettings.json` | Google Client ID, Client Secret, Secret Key, Domain Policies |
| `Saed.Api/appsettings.json` | Secret Key (must match Auth) |

## 🔒 Security Notes

- Never commit real secrets to Git
- The secret keys in appsettings.json are placeholders
- For production, use Azure Key Vault or environment variables
- Keep your Google OAuth credentials secure

## ❓ Troubleshooting

### "The provided antiforgery token was meant for a different claims-based user"
- Clear browser cookies
- Try incognito/private mode

### "401 Unauthorized" when calling API
- Verify secret key is the same in both appsettings.json
- Check that JWT token is not expired
- Ensure token is properly copied (no extra spaces)

### "Failed to load resource" / CORS errors
- Verify all three applications are running
- Check they're on the correct ports (5001, 5002, 3000)
- Clear browser cache

### Google authentication fails
- Verify redirect URI is exactly: `https://localhost:5001/signin-google`
- Check Client ID and Secret are correct
- Ensure Google+ API is enabled

## 📚 Need More Help?

See detailed documentation:
- `README.md` - Project overview
- `SETUP.md` - Detailed setup guide
- `ARCHITECTURE.md` - System architecture
- `PROJECT_SUMMARY.md` - Complete implementation summary

---

**Total Configuration Time:** ~15 minutes

**Happy coding! 🚀**
