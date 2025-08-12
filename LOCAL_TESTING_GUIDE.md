# Local Testing Guide - Entra ID Authentication

This guide shows you how to test the Entra ID authentication locally before deploying to Azure.

## 🔧 Prerequisites

1. **Azure AD Tenant** with admin access
2. **Visual Studio** or **VS Code** with .NET 9.0 SDK
3. **Local development environment** set up

## 📋 Step 1: Configure Azure AD App Registration

### 1.1 Create/Update App Registration

1. Go to **Azure Portal > Azure Active Directory > App registrations**
2. Create new registration or select existing one:
   - **Name**: `AI Document Chat - Local Dev`
   - **Supported account types**: Accounts in this organizational directory only
   - **Redirect URI**: Leave blank for now

### 1.2 Configure Authentication

1. Go to **Authentication** section
2. **Add platform** → **Single-page application**
3. **Add redirect URIs**:
   ```
   http://localhost:5239
   https://localhost:7175
   ```
4. **Under Implicit grant and hybrid flows**, check:
   - ✅ Access tokens (used for implicit flows)
   - ✅ ID tokens (used for implicit and hybrid flows)

### 1.3 Configure API Permissions

1. Go to **API permissions**
2. **Add a permission** → **Microsoft Graph** → **Delegated permissions**
3. Add these permissions:
   - `openid`
   - `profile` 
   - `User.Read`
   - `email`
4. **Grant admin consent** for your organization

### 1.4 Note Configuration Values

Copy these values for your configuration:
- **Application (client) ID**: `xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx`
- **Directory (tenant) ID**: `xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx`
- **Domain**: `your-organization.onmicrosoft.com`

## ⚙️ Step 2: Configure Local Development

### 2.1 Create Development Configuration

1. **Copy the template**:
   ```bash
   copy Backend\appsettings.Development.template.json Backend\appsettings.Development.json
   ```

2. **Update `appsettings.Development.json`** with your values:
   ```json
   {
     "EntraId": {
       "TenantId": "your-tenant-id-here",
       "ClientId": "your-client-id-here",
       "Instance": "https://login.microsoftonline.com/",
       "Domain": "your-organization.onmicrosoft.com",
       "Audience": "api://your-client-id-here"
     }
   }
   ```

### 2.2 Update Frontend Configuration

**Edit `Backend\wwwroot\js\auth.js`** and update the MSAL configuration:

```javascript
const msalConfig = {
    auth: {
        clientId: 'your-client-id-here', // Replace with your Client ID
        authority: 'https://login.microsoftonline.com/your-tenant-id-here', // Replace with your Tenant ID
        redirectUri: window.location.origin
    },
    cache: {
        cacheLocation: 'localStorage',
        storeAuthStateInCookie: false
    }
};

const apiRequest = {
    scopes: ['api://your-client-id-here/access_as_user'] // Replace with your Client ID
};
```

## 🚀 Step 3: Run and Test Locally

### 3.1 Start the Application

```bash
cd Backend
dotnet run --urls "http://localhost:5239;https://localhost:7175"
```

### 3.2 Test Authentication Flow

1. **Open browser** to `https://localhost:7175`
2. **You should see** the loading screen, then login prompt
3. **Click "Sign In with Microsoft"**
4. **You'll be redirected** to Microsoft login page
5. **Sign in** with your organizational account
6. **Grant consent** if prompted
7. **You should be redirected back** to the application
8. **Verify** you see the main chat interface with your name in the header

### 3.3 Test API Functionality

1. **Test basic chat** - send a message without uploading a document
2. **Test document upload** - upload a PDF/DOCX file and ask questions
3. **Check browser dev tools** - verify JWT tokens are being sent in requests
4. **Test logout** - click the logout button and verify you're signed out

## 🔍 Troubleshooting Local Testing

### Common Issues and Solutions

**1. "MSAL configuration not set" Error**
```
Solution: Update the clientId and authority in auth.js
Check: Ensure values don't have placeholder text
```

**2. Redirect Loop or "Invalid Redirect URI"**
```
Solution: Verify redirect URIs in Azure AD match exactly:
- http://localhost:5239
- https://localhost:7175
Check: No trailing slashes, correct protocol
```

**3. "User consent required" Error**
```
Solution: Grant admin consent in Azure AD
Or: Add user consent in API permissions
```

**4. JWT Token Validation Errors**
```
Solution: Check audience and issuer configuration
Verify: TenantId and ClientId are correct in appsettings
```

**5. CORS Errors**
```
Solution: Ensure you're using the correct localhost URLs
Check: Browser is accessing https://localhost:7175
```

### Debug Steps

**1. Check Browser Console**
```javascript
// Open browser dev tools and check for errors
// Look for authentication-related messages
```

**2. Verify Token**
```javascript
// In browser console, check if token exists:
console.log(localStorage.getItem('msal.account.keys'));
```

**3. Check Network Tab**
```
- Look for Authorization headers in API requests
- Verify 401 vs 403 errors (authentication vs authorization)
```

**4. Backend Logs**
```bash
# Check console output for authentication errors
# Look for JWT validation messages
```

## 🧪 Testing Scenarios

### Scenario 1: First-Time User
1. Clear browser cache/localStorage
2. Navigate to app
3. Should see login screen
4. Complete authentication flow

### Scenario 2: Returning User
1. With valid session, navigate to app
2. Should automatically sign in (silent auth)
3. No login prompt should appear

### Scenario 3: Expired Session
1. Wait for token expiration (or manually clear tokens)
2. Try to use the app
3. Should automatically refresh tokens or prompt for login

### Scenario 4: API Authorization
1. Sign in successfully
2. Try chat functionality
3. Upload document and test
4. All API calls should include Bearer tokens

## 📝 Configuration Checklist

- [ ] Azure AD app registration created
- [ ] Redirect URIs configured for localhost
- [ ] API permissions granted
- [ ] Admin consent provided
- [ ] `appsettings.Development.json` updated
- [ ] `auth.js` configuration updated
- [ ] Application runs on correct ports
- [ ] Authentication flow works
- [ ] API calls include JWT tokens
- [ ] Document upload works with auth

## 🔄 Development Workflow

1. **Make code changes**
2. **Test locally** with authentication
3. **Verify all functionality** works
4. **Deploy to Azure** using blue/green process
5. **Update Azure AD** redirect URIs for production

## 🎯 Next Steps

Once local testing is successful:
1. **Update production Azure AD** app registration
2. **Deploy to Azure** using the deployment guide
3. **Test in production** environment
4. **Set up monitoring** and alerts

This local testing approach allows you to:
- ✅ **Develop and debug** authentication issues quickly
- ✅ **Test user experience** before deployment
- ✅ **Verify API integration** works correctly
- ✅ **Iterate rapidly** on authentication features
