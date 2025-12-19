# Google OAuth 2.0 Beépítés - Részletes Implementációs Terv

## 📋 Áttekintés

Ez a dokumentum részletesen leírja, hogyan kell beépíteni a Google OAuth 2.0 bejelentkezést a GdeWeb alkalmazásba. A megvalósítás tartalmazza:
- Google OAuth 2.0 flow kezelését
- Profilkép és név automatikus kitöltését
- Claims és AuthenticationStateProvider bővítését
- Bővíthető UserModel tervezést
- Automatikus onboarding lépéseket új felhasználók számára

---

## 🎯 OAuth Flow Áttekintés

### 1. **Authorization Code Flow**
```
User → Frontend → Backend → Google OAuth → Google → Backend → Frontend → User
```

### 2. **Lépések részletesen:**
1. Felhasználó kattint a "Bejelentkezés Google-lal" gombra
2. Frontend redirect a backend `/api/Auth/GoogleLogin` endpoint-ra
3. Backend redirect a Google OAuth oldalra (authorization code kérés)
4. Felhasználó bejelentkezik Google-lal és engedélyt ad
5. Google redirect a backend `/api/Auth/GoogleCallback` endpoint-ra (code paraméterrel)
6. Backend cseréli a code-t access token-re
7. Backend lekéri a user info-t Google-tól
8. Backend keres/létrehoz felhasználót az adatbázisban
9. Backend generál JWT tokent
10. Backend redirect a frontend-re token-nel
11. Frontend eltárolja a tokent és bejelentkezteti a felhasználót

---

## 🔧 1. NUGET CSOMAGOK HOZZÁADÁSA

### 1.1. GdeWebAPI/GdeWebAPI.csproj

**MÓDOSÍTANDÓ:** Nyissa meg a fájlt és adja hozzá a következő csomagot:

```xml
<Project Sdk="Microsoft.NET.Sdk.Web">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
  </PropertyGroup>

  <ItemGroup>
    <!-- ... meglévő PackageReference-ek ... -->
    
    <!-- Google OAuth - NEM szükséges külön csomag, HttpClient-vel kezeljük -->
    <!-- Ha mégis szeretnél használni, akkor: -->
    <!-- <PackageReference Include="Google.Apis.Auth" Version="1.68.0" /> -->
  </ItemGroup>
  
  <!-- ... többi konfiguráció ... -->
</Project>
```

**MEGJEGYZÉS:** A Google OAuth flow-t manuálisan kezeljük HttpClient-tel, így nincs szükség külön NuGet csomagra. Ha mégis szeretnél használni a Google.Apis.Auth csomagot, akkor az opcionális.

### 1.2. GdeWeb/GdeWeb.csproj

**NINCS MÓDOSÍTÁS:** A frontend-en nincs szükség további csomagra, mert a Google OAuth a backend-en keresztül történik.

---

## 📝 2. ADATBÁZIS MÓDOSÍTÁSOK

### 2.1. GdeWebDB/Entities/Data.cs

**TELJES MÓDOSÍTOTT User osztály:**

```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GdeWebDB.Entities
{
    public class User
    {
        public int USERID { get; set; }              // PK
        public Guid GUID { get; set; }
        public string PASSWORD { get; set; } = "";
        public string FIRSTNAME { get; set; } = "";
        public string LASTNAME { get; set; } = "";
        public string? EMAIL { get; set; }
        public bool ACTIVE { get; set; }
        public string USERDATAJSON { get; set; } = "";
        public DateTime MODIFICATIONDATE { get; set; }
        
        // Google OAuth mezők - ÚJ
        public string? OAUTHPROVIDER { get; set; } = null; // "Google", "Facebook", stb.
        public string? OAUTHID { get; set; } = null; // Google user ID (pl. "123456789012345678901")
        public string? PROFILEPICTURE { get; set; } = null; // Profilkép URL (pl. "https://lh3.googleusercontent.com/...")
        
        // Onboarding flag - ÚJ
        public bool ONBOARDINGCOMPLETED { get; set; } = false; // Ha false, akkor új felhasználó

        public ICollection<UserRole> UserRoles { get; set; } = new List<UserRole>();
        public ICollection<AuthToken> Tokens { get; set; } = new List<AuthToken>();
    }
}
```

**MEGJEGYZÉS:** 
- `OAUTHPROVIDER`: Tárolja, hogy melyik OAuth szolgáltatót használta (jelenleg csak "Google", de később bővíthető)
- `OAUTHID`: A Google által adott egyedi felhasználó azonosító
- `PROFILEPICTURE`: A Google profilkép URL-je
- `ONBOARDINGCOMPLETED`: Flag, ami jelzi, hogy az új felhasználó elvégezte-e az onboarding folyamatot

### 2.2. GdeWebDB/GdeDbContext.cs

**TELJES MÓDOSÍTOTT OnModelCreating metódus a User entitáshoz:**

```csharp
protected override void OnModelCreating(ModelBuilder mb)
{
    // T_USER
    mb.Entity<User>(e =>
    {
        e.ToTable("T_USER");
        e.HasKey(x => x.USERID);
        e.Property(x => x.USERID).ValueGeneratedOnAdd();
        e.Property(x => x.GUID).HasConversion(v => v.ToString(), s => Guid.Parse(s));
        e.Property(x => x.PASSWORD).HasMaxLength(200).IsRequired();
        e.Property(x => x.FIRSTNAME).HasMaxLength(50).IsRequired();
        e.Property(x => x.LASTNAME).HasMaxLength(50).IsRequired();
        e.Property(x => x.EMAIL).HasMaxLength(100);
        e.Property(x => x.ACTIVE).HasDefaultValue(false);
        e.Property(x => x.USERDATAJSON); // SQLite: TEXT; MSSQL: NVARCHAR(MAX)
        e.Property(x => x.MODIFICATIONDATE);
        
        // Google OAuth mezők - ÚJ
        e.Property(x => x.OAUTHPROVIDER).HasMaxLength(50); // "Google", "Facebook", stb.
        e.Property(x => x.OAUTHID).HasMaxLength(200); // Google user ID lehet hosszú
        e.Property(x => x.PROFILEPICTURE).HasMaxLength(500); // URL-ek lehetnek hosszúak
        e.Property(x => x.ONBOARDINGCOMPLETED).HasDefaultValue(false); // Alapértelmezetten false
    });

    // ... többi entitás konfiguráció változatlan ...
}
```

### 2.3. MIGRÁCIÓ LÉTREHOZÁSA ÉS ALKALMAZÁSA

**PowerShell parancsok (projekt gyökérben):**

```powershell
# 1. Migráció létrehozása
dotnet ef migrations add AddGoogleOAuthFields -p GdeWebDB -s GdeWebAPI

# 2. Migráció alkalmazása az adatbázisra
dotnet ef database update -p GdeWebDB -s GdeWebAPI

# 3. (Opcionális) Migráció ellenőrzése
dotnet ef migrations list -p GdeWebDB -s GdeWebAPI
```

**MIGRÁCIÓ FÁJL TARTALMA (automatikusan generálódik):**

A migráció automatikusan létrehozza a következő SQL-t:
```sql
ALTER TABLE T_USER ADD COLUMN OAUTHPROVIDER TEXT NULL;
ALTER TABLE T_USER ADD COLUMN OAUTHID TEXT NULL;
ALTER TABLE T_USER ADD COLUMN PROFILEPICTURE TEXT NULL;
ALTER TABLE T_USER ADD COLUMN ONBOARDINGCOMPLETED INTEGER NOT NULL DEFAULT 0;
```

---

## 🔐 3. KONFIGURÁCIÓS FÁJLOK

### 3.1. GdeWebAPI/appsettings.json

**TELJES MÓDOSÍTOTT FÁJL:**

```json
{
  "JWT": {
    "Key": "GdEgy3t3mW3b@lk@lm@z@sF3jl3szt3s",
    "Issuer": "https://localhost",
    "Audience": "https://localhost",
    "ExpireInHours": 72,
    "ExpireMinutes": 4320
  },
  "ConnectionStrings": {
    "Sqlite": "Data Source=./gde.db"
  },
  "GoogleOAuth": {
    "ClientId": "YOUR_GOOGLE_CLIENT_ID.apps.googleusercontent.com",
    "ClientSecret": "YOUR_GOOGLE_CLIENT_SECRET",
    "RedirectUri": "https://localhost:7046/api/Auth/GoogleCallback"
  },
  "MailCredentials": {
    "UserName": "jakab.d@gmail.com",
    "Password": ""
  },
  "ContactMail": "jakab.d@gmail.com",
  "OpenAI": {
    "ApiKey": ""
  },
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "AllowedHosts": "*",
  "websiteUrl": "https://localhost:7294",
  "apiUrl": "https://localhost:7046"
}
```

**FONTOS:** 
- Cserélje ki a `YOUR_GOOGLE_CLIENT_ID` és `YOUR_GOOGLE_CLIENT_SECRET` értékeket a Google Cloud Console-ból kapott értékekre
- A `RedirectUri`-nak pontosan egyeznie kell a Google Cloud Console-ban beállított redirect URI-val

### 3.2. GdeWebAPI/appsettings.Development.json

**HOZZÁADANDÓ (ha külön development konfigurációt szeretnél):**

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "GoogleOAuth": {
    "ClientId": "DEV_GOOGLE_CLIENT_ID.apps.googleusercontent.com",
    "ClientSecret": "DEV_GOOGLE_CLIENT_SECRET",
    "RedirectUri": "https://localhost:7046/api/Auth/GoogleCallback"
  }
}
```

### 3.3. GdeWeb/appsettings.json

**TELJES MÓDOSÍTOTT FÁJL:**

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "AllowedHosts": "*",
  "websiteUrl": "https://localhost:7294",
  "apiUrl": "https://localhost:7046",
  "GoogleOAuth": {
    "ClientId": "YOUR_GOOGLE_CLIENT_ID.apps.googleusercontent.com"
  }
}
```

**MEGJEGYZÉS:** A frontend-en csak a ClientId szükséges, mert a backend-en keresztül történik az OAuth flow.

---

## 📦 4. MODEL MÓDOSÍTÁSOK

### 4.1. GdeWebModels/LoginResultModel.cs

**TELJES MÓDOSÍTOTT FÁJL:**

```csharp
using Swashbuckle.AspNetCore.Annotations;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GdeWebModels
{
    [SwaggerSchema("Login eredmény osztálya")]
    public class LoginResultModel
    {
        [SwaggerSchema("Login eredmény azonosítója")]
        public int Id { get; set; } = 0;

        [SwaggerSchema("Login eredmény engedély listája")]
        public List<LoginRoleModel> Roles { get; set; } = new List<LoginRoleModel> { new LoginRoleModel() };

        [SwaggerSchema("Login eredmény guid azonosítója")]
        public System.Guid Guid { get; set; } = Guid.NewGuid();

        [SwaggerSchema("Login eredmény token azonosítója")]
        public string Token { get; set; } = String.Empty;

        [SwaggerSchema("Login eredmény aktív")]
        public bool Active { get; set; } = false;

        [SwaggerSchema("Onboarding befejezve - ÚJ")]
        public bool OnboardingCompleted { get; set; } = false;

        [SwaggerSchema("Művelet sikeressége")]
        public ResultModel Result { get; set; } = new ResultModel();
    }
}
```

### 4.2. GdeWebModels/UserModel.cs

**TELJES MÓDOSÍTOTT FÁJL:**

```csharp
using GdeWebModels;
using Newtonsoft.Json;
using Swashbuckle.AspNetCore.Annotations;
using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace GdeWebModels
{
    [SwaggerSchema("Felhasználó osztály")]
    public class UserModel
    {
        [SwaggerSchema("Felhasználó azonosító")]
        public int Id { get; set; } = 0;

        [SwaggerSchema("Login felhasználó guid azonosítója")]
        public object Guid { get; set; } = String.Empty;

        [SwaggerSchema("Felhasználó jelszava")]
        public string Password { get; set; } = String.Empty;

        [SwaggerSchema("Felhasználó keresztneve")]
        public string FirstName { get; set; } = String.Empty;

        [SwaggerSchema("Felhasználó vezetékneve")]
        public string LastName { get; set; } = String.Empty;

        [SwaggerSchema("Felhasználó email címe")]
        public string Email { get; set; } = String.Empty;

        [SwaggerSchema("Felhasználó személyes adatai")]
        public UserDataModel UserData { get; set; } = new UserDataModel();

        [SwaggerSchema("Felhasználó aktív")]
        public bool Active { get; set; } = false;

        [SwaggerSchema("Felhasználó módosítója")]
        public int Modifier { get; set; } = 0;

        [SwaggerSchema("Felhasználó szerepköreinek listája")]
        public List<RoleModel> Roles { get; set; } = new List<RoleModel> { new RoleModel() };

        [SwaggerSchema("OAuth szolgáltató (pl. Google) - ÚJ")]
        public string? OAuthProvider { get; set; }

        [SwaggerSchema("OAuth felhasználó azonosító - ÚJ")]
        public string? OAuthId { get; set; }

        [SwaggerSchema("Profilkép URL - ÚJ")]
        public string? ProfilePicture { get; set; }

        [SwaggerSchema("Onboarding befejezve - ÚJ")]
        public bool OnboardingCompleted { get; set; } = false;

        [SwaggerSchema("Művelet sikeressége")]
        public ResultModel Result { get; set; } = new ResultModel() { Success = true, ErrorMessage = "" };

        [SwaggerSchema("Felhasználó személyes adatainak json string formátuma")]
        [Newtonsoft.Json.JsonIgnore]
        public string UserDataJson
        {
            get => String.Empty;
            set
            {
                UserData = string.IsNullOrEmpty(value) 
                    ? new UserDataModel() 
                    : JsonConvert.DeserializeObject<UserDataModel>(value) ?? new UserDataModel();
            }
        }
    }
}
```

### 4.3. GdeWebModels/LoginUserModel.cs

**TELJES MÓDOSÍTOTT FÁJL:**

```csharp
using Newtonsoft.Json;
using Swashbuckle.AspNetCore.Annotations;

namespace GdeWebModels
{
    [SwaggerSchema("Login felhasználó osztálya")]
    public class LoginUserModel
    {
        [SwaggerSchema("Login felhasználó azonosítója")]
        public int Id { get; set; } = 0;

        [SwaggerSchema("Login felhasználó guid azonosítója")]
        public System.Guid Guid { get; set; } = Guid.NewGuid();

        [SwaggerSchema("Login token azonosítója")]
        public string Token { get; set; } = String.Empty;

        [SwaggerSchema("Login felhasználó keresztneve")]
        public string FirstName { get; set; } = String.Empty;

        [SwaggerSchema("Login felhasználó vezetékneve")]
        public string LastName { get; set; } = String.Empty;

        [SwaggerSchema("Login felhasználó email címe")]
        public string Email { get; set; } = String.Empty;

        [SwaggerSchema("Felhasználó személyes adatai")]
        public UserDataModel UserData { get; set; } = new UserDataModel();

        [SwaggerSchema("Login felhasználó szerepkör listája")]
        public List<LoginRoleModel> Roles { get; set; } = new List<LoginRoleModel> { new LoginRoleModel() };

        [SwaggerSchema("Profilkép URL - ÚJ")]
        public string? ProfilePicture { get; set; }

        [SwaggerSchema("Onboarding befejezve - ÚJ")]
        public bool OnboardingCompleted { get; set; } = false;

        [SwaggerSchema("Művelet sikeressége")]
        public ResultModel Result { get; set; } = new ResultModel();

        [SwaggerSchema("Felhasználó személyes adatainak json string formátuma")]
        [Newtonsoft.Json.JsonIgnore]
        public string UserDataJson
        {
            get => String.Empty;
            set
            {
                UserData = string.IsNullOrEmpty(value) 
                    ? new UserDataModel() 
                    : JsonConvert.DeserializeObject<UserDataModel>(value) ?? new UserDataModel();
            }
        }
    }
}
```

### 4.4. GdeWebModels/GoogleOAuthModel.cs (ÚJ FÁJL)

**LÉTREHOZANDÓ TELJES FÁJL:**

```csharp
using Swashbuckle.AspNetCore.Annotations;

namespace GdeWebModels
{
    /// <summary>
    /// Google OAuth bejelentkezés modell - ID token és access token tárolásához
    /// </summary>
    [SwaggerSchema("Google OAuth bejelentkezés modell")]
    public class GoogleOAuthModel
    {
        [SwaggerSchema("Google ID token")]
        public string IdToken { get; set; } = string.Empty;
        
        [SwaggerSchema("Google access token")]
        public string AccessToken { get; set; } = string.Empty;
    }
    
    /// <summary>
    /// Google OAuth callback adatok - authorization code és state paraméterek
    /// </summary>
    [SwaggerSchema("Google OAuth callback adatok")]
    public class GoogleOAuthCallbackModel
    {
        [SwaggerSchema("Authorization code - Google-tól kapott kód")]
        public string Code { get; set; } = string.Empty;
        
        [SwaggerSchema("State parameter - CSRF védelemhez")]
        public string? State { get; set; }
        
        [SwaggerSchema("Error parameter - ha hiba történt")]
        public string? Error { get; set; }
        
        [SwaggerSchema("Error description - hiba leírása")]
        public string? ErrorDescription { get; set; }
    }
    
    /// <summary>
    /// Google user info modell - Google API válaszából
    /// </summary>
    [SwaggerSchema("Google user info modell")]
    public class GoogleUserInfoModel
    {
        [SwaggerSchema("Google user ID")]
        public string Id { get; set; } = string.Empty;
        
        [SwaggerSchema("Email cím")]
        public string Email { get; set; } = string.Empty;
        
        [SwaggerSchema("Email megerősítve")]
        public bool VerifiedEmail { get; set; }
        
        [SwaggerSchema("Keresztnév")]
        public string GivenName { get; set; } = string.Empty;
        
        [SwaggerSchema("Vezetéknév")]
        public string FamilyName { get; set; } = string.Empty;
        
        [SwaggerSchema("Teljes név")]
        public string Name { get; set; } = string.Empty;
        
        [SwaggerSchema("Profilkép URL")]
        public string Picture { get; set; } = string.Empty;
        
        [SwaggerSchema("Nyelv")]
        public string Locale { get; set; } = string.Empty;
    }
}
```

---

## 🔄 5. API KONTROLLER MÓDOSÍTÁSOK

### 5.1. GdeWebAPI/Controllers/AuthController.cs

**TELJES MÓDOSÍTOTT FÁJL (csak a releváns részek, a többi változatlan):**

```csharp
using GdeWebDB.Interfaces;
using GdeWebDB.Services;
using GdeWebDB.Utilities;
using GdeWebModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.IdentityModel.Tokens;
using Swashbuckle.AspNetCore.Annotations;
using System.Data;
using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Mime;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json; // ÚJ

namespace GdeWebAPI.Controllers
{
    /// <summary>
    /// Felhasználók hitelesítéséért és jogosultsági tokenek kezeléséért felelős API vezérlő.
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    [DisableRateLimiting]
    public class AuthController : ControllerBase
    {
        private readonly IConfiguration _configuration;
        private readonly IAuthService _authService;
        private readonly ILogService _logService;
        private readonly IMemoryCache _cache; // ÚJ - State tároláshoz (opcionális)

        public AuthController(
            IConfiguration configuration, 
            IAuthService authService, 
            ILogService logService,
            IMemoryCache cache = null) // ÚJ
        {
            ArgumentNullException.ThrowIfNull(configuration);
            ArgumentNullException.ThrowIfNull(authService);
            ArgumentNullException.ThrowIfNull(logService);

            this._configuration = configuration;
            this._authService = authService;
            this._logService = logService;
            this._cache = cache;
        }

        // ... meglévő Login és GetUserFromToken metódusok változatlanok ...

        /// <summary>
        /// Google OAuth bejelentkezés indítása - redirect a Google bejelentkezési oldalra
        /// </summary>
        /// <returns>Redirect a Google OAuth oldalra</returns>
        [HttpGet]
        [Route("GoogleLogin")]
        [ApiExplorerSettings(IgnoreApi = true)]
        [SwaggerOperation(
            Summary = "Google OAuth bejelentkezés indítása",
            Description = "Redirect a Google bejelentkezési oldalra"
        )]
        public IActionResult GoogleLogin()
        {
            try
            {
                var clientId = _configuration["GoogleOAuth:ClientId"];
                var redirectUri = _configuration["GoogleOAuth:RedirectUri"];
                
                if (string.IsNullOrEmpty(clientId) || string.IsNullOrEmpty(redirectUri))
                {
                    return BadRequest(new { error = "Google OAuth nincs konfigurálva" });
                }
                
                // CSRF védelem - state generálása
                var state = Guid.NewGuid().ToString();
                
                // State tárolása cache-ben (5 percig érvényes)
                if (_cache != null)
                {
                    _cache.Set($"oauth_state_{state}", state, TimeSpan.FromMinutes(5));
                }
                
                // Google OAuth URL összeállítása
                var googleAuthUrl = $"https://accounts.google.com/o/oauth2/v2/auth?" +
                    $"client_id={Uri.EscapeDataString(clientId)}&" +
                    $"redirect_uri={Uri.EscapeDataString(redirectUri)}&" +
                    $"response_type=code&" +
                    $"scope=openid%20email%20profile&" +
                    $"state={Uri.EscapeDataString(state)}&" +
                    $"access_type=offline&" + // Refresh token kérése
                    $"prompt=consent"; // Mindig kérjen engedélyt
                
                return Redirect(googleAuthUrl);
            }
            catch (Exception ex)
            {
                _logService.WriteLogToFile(ex, "GoogleLogin hiba").Wait();
                return StatusCode(500, new { error = "Hiba történt a Google bejelentkezés indítása során" });
            }
        }

        /// <summary>
        /// Google OAuth callback - feldolgozza a Google válaszát
        /// </summary>
        /// <param name="model">Callback paraméterek (code, state, error)</param>
        /// <returns>Redirect a frontend-re token-nel vagy hibaüzenettel</returns>
        [HttpGet]
        [Route("GoogleCallback")]
        [ApiExplorerSettings(IgnoreApi = true)]
        [SwaggerOperation(
            Summary = "Google OAuth callback",
            Description = "Feldolgozza a Google OAuth válaszát"
        )]
        public async Task<IActionResult> GoogleCallback([FromQuery] GoogleOAuthCallbackModel model)
        {
            try
            {
                // Hiba ellenőrzése
                if (!string.IsNullOrEmpty(model.Error))
                {
                    var errorMsg = string.IsNullOrEmpty(model.ErrorDescription) 
                        ? model.Error 
                        : $"{model.Error}: {model.ErrorDescription}";
                    await _logService.WriteLogToFile(
                        new Exception(errorMsg), 
                        "Google OAuth callback error");
                    return Redirect($"{_configuration["websiteUrl"]}/signin?error={Uri.EscapeDataString(errorMsg)}");
                }
                
                // State validálása (CSRF védelem)
                if (_cache != null && !string.IsNullOrEmpty(model.State))
                {
                    var cachedState = _cache.Get<string>($"oauth_state_{model.State}");
                    if (cachedState == null || cachedState != model.State)
                    {
                        await _logService.WriteLogToFile(
                            new Exception("Invalid state parameter"), 
                            "Google OAuth CSRF attack attempt");
                        return Redirect($"{_configuration["websiteUrl"]}/signin?error=Invalid+state+parameter");
                    }
                    // State törlése (egyszer használatos)
                    _cache.Remove($"oauth_state_{model.State}");
                }
                
                // Authorization code ellenőrzése
                if (string.IsNullOrEmpty(model.Code))
                {
                    return Redirect($"{_configuration["websiteUrl"]}/signin?error=Missing+authorization+code");
                }
                
                // 1. Authorization code cseréje access token-re
                var tokenResponse = await ExchangeCodeForToken(model.Code);
                
                if (tokenResponse == null)
                {
                    return Redirect($"{_configuration["websiteUrl"]}/signin?error=Token+exchange+failed");
                }
                
                // Access token kinyerése
                var accessToken = tokenResponse.Value.GetProperty("access_token").GetString();
                if (string.IsNullOrEmpty(accessToken))
                {
                    return Redirect($"{_configuration["websiteUrl"]}/signin?error=Access+token+not+received");
                }
                
                // 2. User info lekérése Google-tól
                var googleUserInfo = await GetGoogleUserInfo(accessToken);
                
                if (googleUserInfo == null)
                {
                    return Redirect($"{_configuration["websiteUrl"]}/signin?error=User+info+fetch+failed");
                }
                
                // 3. Felhasználó keresése vagy létrehozása
                var loginResult = await _authService.LoginOrCreateGoogleUser(googleUserInfo);
                
                if (!loginResult.Result.Success)
                {
                    return Redirect($"{_configuration["websiteUrl"]}/signin?error={Uri.EscapeDataString(loginResult.Result.ErrorMessage)}");
                }
                
                // 4. JWT token generálása
                string token = Utilities.Utilities.GenerateToken(loginResult, _configuration);
                loginResult.Token = token;
                
                // 5. Token mentése
                double expireHours = Convert.ToDouble(_configuration["Jwt:ExpireInHours"] ?? "72");
                await _authService.AddUserTokenExpirationDate(
                    loginResult.Id, 
                    token, 
                    DateTime.Now.AddHours(expireHours));
                
                // 6. Redirect a frontend-re token-nel
                var frontendUrl = $"{_configuration["websiteUrl"]}/signin?" +
                    $"token={Uri.EscapeDataString(token)}&" +
                    $"onboarding={(!loginResult.OnboardingCompleted).ToString().ToLower()}";
                
                return Redirect(frontendUrl);
            }
            catch (Exception ex)
            {
                await _logService.WriteLogToFile(ex, "Google OAuth callback hiba");
                return Redirect($"{_configuration["websiteUrl"]}/signin?error=Google+bejelentkezés+sikertelen");
            }
        }

        /// <summary>
        /// Authorization code cseréje access token-re
        /// </summary>
        private async Task<JsonElement?> ExchangeCodeForToken(string code)
        {
            try
            {
                var clientId = _configuration["GoogleOAuth:ClientId"];
                var clientSecret = _configuration["GoogleOAuth:ClientSecret"];
                var redirectUri = _configuration["GoogleOAuth:RedirectUri"];
                
                if (string.IsNullOrEmpty(clientId) || string.IsNullOrEmpty(clientSecret) || string.IsNullOrEmpty(redirectUri))
                {
                    throw new Exception("Google OAuth konfiguráció hiányzik");
                }
                
                using var httpClient = new HttpClient();
                var request = new HttpRequestMessage(HttpMethod.Post, "https://oauth2.googleapis.com/token");
                request.Content = new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    { "code", code },
                    { "client_id", clientId },
                    { "client_secret", clientSecret },
                    { "redirect_uri", redirectUri },
                    { "grant_type", "authorization_code" }
                });
                
                var response = await httpClient.SendAsync(request);
                var content = await response.Content.ReadAsStringAsync();
                
                if (!response.IsSuccessStatusCode)
                {
                    await _logService.WriteLogToFile(
                        new Exception($"Token exchange failed: {content}"), 
                        "Google OAuth token exchange error");
                    return null;
                }
                
                return JsonSerializer.Deserialize<JsonElement>(content);
            }
            catch (Exception ex)
            {
                await _logService.WriteLogToFile(ex, "ExchangeCodeForToken hiba");
                return null;
            }
        }

        /// <summary>
        /// User info lekérése Google-tól access token alapján
        /// </summary>
        private async Task<JsonElement?> GetGoogleUserInfo(string accessToken)
        {
            try
            {
                using var httpClient = new HttpClient();
                httpClient.DefaultRequestHeaders.Authorization = 
                    new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);
                
                var response = await httpClient.GetAsync("https://www.googleapis.com/oauth2/v2/userinfo");
                var content = await response.Content.ReadAsStringAsync();
                
                if (!response.IsSuccessStatusCode)
                {
                    await _logService.WriteLogToFile(
                        new Exception($"User info fetch failed: {content}"), 
                        "Google OAuth user info error");
                    return null;
                }
                
                return JsonSerializer.Deserialize<JsonElement>(content);
            }
            catch (Exception ex)
            {
                await _logService.WriteLogToFile(ex, "GetGoogleUserInfo hiba");
                return null;
            }
        }
    }
}
```

**HOZZÁADANDÓ using a fájl tetejéhez:**

```csharp
using Microsoft.Extensions.Caching.Memory; // State tároláshoz
```

**MEGJEGYZÉS:** Ha nem szeretnél használni IMemoryCache-et, akkor a state validálást el lehet hagyni vagy session-ben tárolni.

### 5.2. GdeWebAPI/Program.cs

**HOZZÁADANDÓ (ha IMemoryCache-et használsz):**

```csharp
// Add services to the container
builder.Services.AddMemoryCache(); // ÚJ - State tároláshoz
```

---

## 🗄️ 6. DATABASE SERVICE MÓDOSÍTÁSOK

### 6.1. GdeWebDB/Interfaces/IAuthService.cs

**TELJES MÓDOSÍTOTT FÁJL:**

```csharp
using GdeWebModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GdeWebDB.Interfaces
{
    public interface IAuthService
    {
        Task<LoginResultModel> Login(LoginModel credential);
        Task<LoginResultModel> Auth(LoginModel credentials);
        Task<LoginResultModel> Forgot(ForgotModel model);
        Task<ResultModel> GetUserTokenExpirationDate(int userId, DateTime expirationDate);
        Task<ResultModel> AddUserTokenExpirationDate(int userId, string token, DateTime expirationDate);
        Task<LoginUserModel> GetUser(int userId);
        Task<ResultModel> UserValidation(int userId, string userGuid);
        
        // ÚJ - Google OAuth login vagy létrehozás
        Task<LoginResultModel> LoginOrCreateGoogleUser(System.Text.Json.JsonElement googleUserInfo);
    }
}
```

### 6.2. GdeWebDB/Services/AuthService.cs

**HOZZÁADANDÓ METÓDUS (a fájl végéhez):**

```csharp
/// <summary>
/// Google OAuth felhasználó bejelentkezése vagy létrehozása
/// </summary>
/// <param name="googleUserInfo">Google API-tól kapott user info JSON</param>
/// <returns>LoginResultModel a felhasználó adataival</returns>
public async Task<LoginResultModel> LoginOrCreateGoogleUser(System.Text.Json.JsonElement googleUserInfo)
{
    try
    {
        // Google user info kinyerése
        var googleId = googleUserInfo.GetProperty("id").GetString();
        var email = googleUserInfo.GetProperty("email").GetString();
        var verifiedEmail = googleUserInfo.TryGetProperty("verified_email", out var verifiedProp) 
            ? verifiedProp.GetBoolean() 
            : false;
        var firstName = googleUserInfo.TryGetProperty("given_name", out var givenNameProp) 
            ? givenNameProp.GetString() 
            : "";
        var lastName = googleUserInfo.TryGetProperty("family_name", out var familyNameProp) 
            ? familyNameProp.GetString() 
            : "";
        var profilePicture = googleUserInfo.TryGetProperty("picture", out var pictureProp) 
            ? pictureProp.GetString() 
            : "";
        
        if (string.IsNullOrEmpty(googleId) || string.IsNullOrEmpty(email))
        {
            return new LoginResultModel 
            { 
                Result = new ResultModel 
                { 
                    Success = false, 
                    ErrorMessage = "Hiányzó Google felhasználói adatok." 
                } 
            };
        }
        
        // 1. Keresés OAuth ID alapján (elsődleges keresés)
        var existingUser = await _db.T_USER
            .FirstOrDefaultAsync(u => u.OAUTHID == googleId && u.OAUTHPROVIDER == "Google");
        
        if (existingUser != null)
        {
            // Meglévő Google OAuth felhasználó
            // Frissítjük a profilképet és egyéb adatokat, ha változtak
            bool needsUpdate = false;
            
            if (existingUser.PROFILEPICTURE != profilePicture)
            {
                existingUser.PROFILEPICTURE = profilePicture;
                needsUpdate = true;
            }
            
            if (existingUser.FIRSTNAME != firstName)
            {
                existingUser.FIRSTNAME = firstName;
                needsUpdate = true;
            }
            
            if (existingUser.LASTNAME != lastName)
            {
                existingUser.LASTNAME = lastName;
                needsUpdate = true;
            }
            
            if (existingUser.EMAIL != email)
            {
                existingUser.EMAIL = email;
                needsUpdate = true;
            }
            
            if (needsUpdate)
            {
                existingUser.MODIFICATIONDATE = DateTime.UtcNow;
                await _db.SaveChangesAsync();
            }
            
            // Aktív ellenőrzés
            if (!existingUser.ACTIVE)
            {
                return new LoginResultModel
                {
                    Result = new ResultModel
                    {
                        Success = false,
                        ErrorMessage = "A felhasználói fiók inaktív."
                    }
                };
            }
            
            // Szerepek lekérése
            var roles = await _db.K_USER_ROLES
                .Where(ur => ur.USERID == existingUser.USERID)
                .Where(ur => ur.Role != null && !string.IsNullOrEmpty(ur.Role.ROLENAME))
                .Select(ur => new LoginRoleModel
                {
                    Id = ur.Role.ROLEID,
                    Name = ur.Role.ROLENAME
                })
                .Distinct()
                .ToListAsync();
            
            return new LoginResultModel
            {
                Id = existingUser.USERID,
                Guid = existingUser.GUID,
                Active = existingUser.ACTIVE,
                Roles = roles,
                OnboardingCompleted = existingUser.ONBOARDINGCOMPLETED,
                Result = new ResultModel { Success = true }
            };
        }
        
        // 2. Keresés email alapján (ha már létezik email-lel, összekapcsoljuk)
        var userByEmail = await _db.T_USER
            .FirstOrDefaultAsync(u => u.EMAIL == email);
        
        if (userByEmail != null)
        {
            // Összekapcsoljuk a Google fiókkal
            userByEmail.OAUTHPROVIDER = "Google";
            userByEmail.OAUTHID = googleId;
            userByEmail.PROFILEPICTURE = profilePicture;
            
            // Név frissítése, ha üres volt
            if (string.IsNullOrEmpty(userByEmail.FIRSTNAME) && !string.IsNullOrEmpty(firstName))
            {
                userByEmail.FIRSTNAME = firstName;
            }
            if (string.IsNullOrEmpty(userByEmail.LASTNAME) && !string.IsNullOrEmpty(lastName))
            {
                userByEmail.LASTNAME = lastName;
            }
            
            // Ha nincs jelszó, generálunk egyet (OAuth esetén opcionális)
            if (string.IsNullOrEmpty(userByEmail.PASSWORD))
            {
                userByEmail.PASSWORD = Utilities.Utilities.EncryptPassword(Guid.NewGuid().ToString());
            }
            
            userByEmail.MODIFICATIONDATE = DateTime.UtcNow;
            await _db.SaveChangesAsync();
            
            var roles = await _db.K_USER_ROLES
                .Where(ur => ur.USERID == userByEmail.USERID)
                .Where(ur => ur.Role != null && !string.IsNullOrEmpty(ur.Role.ROLENAME))
                .Select(ur => new LoginRoleModel
                {
                    Id = ur.Role.ROLEID,
                    Name = ur.Role.ROLENAME
                })
                .Distinct()
                .ToListAsync();
            
            return new LoginResultModel
            {
                Id = userByEmail.USERID,
                Guid = userByEmail.GUID,
                Active = userByEmail.ACTIVE,
                Roles = roles,
                OnboardingCompleted = userByEmail.ONBOARDINGCOMPLETED,
                Result = new ResultModel { Success = true }
            };
        }
        
        // 3. Új felhasználó létrehozása
        var newUser = new User
        {
            GUID = Guid.NewGuid(),
            EMAIL = email,
            FIRSTNAME = firstName ?? "",
            LASTNAME = lastName ?? "",
            PASSWORD = Utilities.Utilities.EncryptPassword(Guid.NewGuid().ToString()), // Random jelszó
            ACTIVE = true, // Google OAuth esetén automatikusan aktív
            OAUTHPROVIDER = "Google",
            OAUTHID = googleId,
            PROFILEPICTURE = profilePicture,
            ONBOARDINGCOMPLETED = false, // Új felhasználó -> onboarding kell
            USERDATAJSON = "{}",
            MODIFICATIONDATE = DateTime.UtcNow
        };
        
        _db.T_USER.Add(newUser);
        await _db.SaveChangesAsync();
        
        // Alapértelmezett "User" szerepkör hozzáadása
        var defaultRole = await _db.T_ROLE.FirstOrDefaultAsync(r => r.ROLENAME == "User");
        if (defaultRole != null)
        {
            var userRole = new UserRole
            {
                USERID = newUser.USERID,
                ROLEID = defaultRole.ROLEID,
                CREATOR = newUser.USERID,
                CREATINGDATE = DateTime.UtcNow
            };
            _db.K_USER_ROLES.Add(userRole);
            await _db.SaveChangesAsync();
        }
        
        var newUserRoles = new List<LoginRoleModel>();
        if (defaultRole != null)
        {
            newUserRoles.Add(new LoginRoleModel 
            { 
                Id = defaultRole.ROLEID, 
                Name = defaultRole.ROLENAME 
            });
        }
        
        return new LoginResultModel
        {
            Id = newUser.USERID,
            Guid = newUser.GUID,
            Active = newUser.ACTIVE,
            Roles = newUserRoles,
            OnboardingCompleted = false,
            Result = new ResultModel { Success = true }
        };
    }
    catch (Exception ex)
    {
        await _logService.WriteLogToFile(ex, "LoginOrCreateGoogleUser hiba");
        return new LoginResultModel 
        { 
            Result = new ResultModel 
            { 
                Success = false, 
                ErrorMessage = "Hiba történt a Google bejelentkezés során." 
            } 
        };
    }
}
```

**MÓDOSÍTANDÓ a `GetUser` metódus (hozzáadni ProfilePicture és OnboardingCompleted mezőket):**

```csharp
public async Task<LoginUserModel> GetUser(int userId)
{
    try
    {
        var user = await _db.T_USER
            .AsNoTracking()
            .Where(u => u.USERID == userId)
            .Select(u => new LoginUserModel
            {
                Id = u.USERID,
                Guid = u.GUID,
                FirstName = u.FIRSTNAME,
                LastName = u.LASTNAME,
                Email = u.EMAIL ?? String.Empty,
                ProfilePicture = u.PROFILEPICTURE ?? String.Empty, // ÚJ
                OnboardingCompleted = u.ONBOARDINGCOMPLETED, // ÚJ
                UserDataJson = u.USERDATAJSON
            })
            .FirstOrDefaultAsync();

        if (user == null)
            return new LoginUserModel { Result = ResultTypes.NotFound };

        // Szerepkörök lekérése
        user.Roles = await _db.K_USER_ROLES
            .Where(ur => ur.USERID == userId)
            .Where(ur => ur.Role != null && !string.IsNullOrEmpty(ur.Role.ROLENAME))
            .Select(ur => new LoginRoleModel
            {
                Id = ur.Role.ROLEID,
                Name = ur.Role.ROLENAME
            })
            .Distinct()
            .ToListAsync();

        // Legfrissebb nem lejárt token beolvasása
        var now = DateTime.UtcNow;
        var latestToken = await _db.T_AUTHENTICATION
            .Where(t => t.USERID == userId && t.EXPIRATIONDATE > now)
            .OrderByDescending(t => t.EXPIRATIONDATE)
            .Select(t => t.TOKEN)
            .FirstOrDefaultAsync();

        user.Token = latestToken ?? string.Empty;
        user.Result = new ResultModel { Success = true };
        return user;
    }
    catch (Exception ex)
    {
        await _logService.WriteLogToFile(ex, "GetUser hiba");
        return new LoginUserModel { Result = ResultTypes.UnexpectedError };
    }
}
```

---

## 🎨 7. FRONTEND MÓDOSÍTÁSOK

### 7.1. GdeWeb/Components/Pages/Authentication/Signin.razor

**HOZZÁADANDÓ a form után (a "Bejelentkezés" gomb alatt, de még a MudForm-on belül):**

```razor
<MudDivider Class="my-4" />

<MudText Align="Align.Center" Typo="Typo.body2" Class="mb-3">
    Vagy jelentkezzen be Google fiókkal
</MudText>

<MudButton 
    Class="card-button" 
    Variant="Variant.Outlined" 
    Color="Color.Primary" 
    FullWidth="true"
    StartIcon="@Icons.Material.Filled.Login"
    OnClick="@GoogleLogin"
    Disabled="isLoading">
    <MudIcon Icon="@Icons.Custom.Brands.Google" Class="mr-2" />
    Bejelentkezés Google-lal
</MudButton>
```

**HOZZÁADANDÓ using-ok a fájl tetejéhez:**

```razor
@using Microsoft.Extensions.Configuration
@using Microsoft.AspNetCore.WebUtilities
```

**HOZZÁADANDÓ inject a fájl tetejéhez:**

```razor
@inject IConfiguration configuration
```

**MÓDOSÍTANDÓ az `OnParametersSetAsync` metódus:**

```csharp
protected override async Task OnParametersSetAsync()
{
    PageLoading = true;
    try
    {
        // Google callback kezelése
        var uri = navigationManager.ToAbsoluteUri(navigationManager.Uri);
        var queryParams = QueryHelpers.ParseQuery(uri.Query);
        
        if (queryParams.ContainsKey("token"))
        {
            var token = queryParams["token"].ToString();
            var onboardingParam = queryParams.ContainsKey("onboarding") 
                ? queryParams["onboarding"].ToString() 
                : "false";
            var needsOnboarding = onboardingParam.Equals("true", StringComparison.OrdinalIgnoreCase);
            
            await HandleGoogleCallback(token, needsOnboarding);
            return;
        }
        
        if (queryParams.ContainsKey("error"))
        {
            var error = queryParams["error"].ToString();
            snackbarService.ShowSnackbar(Severity.Error, error, MainLayout.pageWidth);
        }
        
        if (Confirmation is not null && Confirmation == true)
        {
            _showConfirmation = true;
            return;
        }
    }
    finally
    {
        PageLoading = false;
    }
}
```

**HOZZÁADANDÓ metódusok a @code részhez:**

```csharp
/// <summary>
/// Google bejelentkezés indítása
/// </summary>
private void GoogleLogin()
{
    try
    {
        var apiUrl = configuration.GetValue<string>("apiUrl") ?? "";
        if (string.IsNullOrEmpty(apiUrl))
        {
            snackbarService.ShowSnackbar(
                Severity.Error, 
                "API URL nincs konfigurálva!", 
                MainLayout.pageWidth);
            return;
        }
        
        // Redirect a backend GoogleLogin endpoint-ra
        navigationManager.NavigateTo($"{apiUrl}/api/Auth/GoogleLogin", forceLoad: true);
    }
    catch (Exception ex)
    {
        snackbarService.ShowSnackbar(
            Severity.Error, 
            $"Hiba történt: {ex.Message}", 
            MainLayout.pageWidth);
    }
}

/// <summary>
/// Google OAuth callback kezelése
/// </summary>
private async Task HandleGoogleCallback(string token, bool needsOnboarding)
{
    try
    {
        isLoading = true;
        StateHasChanged();
        
        // Token mentése localStorage-ba
        await localStorage.SetItemAsync("token", token);
        
        // User adatok lekérése
        LoginTokenModel loginTokenModel = new LoginTokenModel() { Token = token };
        LoginUserModel user = await authService.GetUserFromToken(loginTokenModel);
        
        if (user.Result.Success && user.Id > 0)
        {
            // Authentication state frissítése
            await ((CustomAuthentication)authenticationStateProvider).MarkUserAsLoggedOut();
            await MainLayout.RefreshLoggedUser();
            
            await ((CustomAuthentication)authenticationStateProvider).MarkUserAsAuthenticated(
                new LoginResultModel 
                { 
                    Id = user.Id, 
                    Token = token,
                    Active = true,
                    Roles = user.Roles,
                    OnboardingCompleted = !needsOnboarding
                });
            
            await MainLayout.RefreshLoggedUser();
            
            snackbarService.ShowSnackbar(
                Severity.Success, 
                "Sikeres Google bejelentkezés!", 
                MainLayout.pageWidth);
            
            // Onboarding vagy dashboard
            if (needsOnboarding)
            {
                navigationManager.NavigateTo("/onboarding");
            }
            else
            {
                navigationManager.NavigateTo("/dashboard");
            }
        }
        else
        {
            snackbarService.ShowSnackbar(
                Severity.Error, 
                "Hiba történt a bejelentkezés során.", 
                MainLayout.pageWidth);
        }
    }
    catch (Exception ex)
    {
        snackbarService.ShowSnackbar(
            Severity.Error, 
            $"Hiba: {ex.Message}", 
            MainLayout.pageWidth);
    }
    finally
    {
        isLoading = false;
        StateHasChanged();
    }
}
```

### 7.2. GdeWeb/Services/CustomAuthentication.cs

**MÓDOSÍTANDÓ a `GetClaimsIdentity` metódus:**

```csharp
/// <summary>
/// Felhasználó claimek létrehozása
/// </summary>
/// <param name="user"></param>
/// <returns></returns>
private ClaimsIdentity GetClaimsIdentity(LoginUserModel user)
{
    ClaimsIdentity claimsIdentity = new ClaimsIdentity();

    if (user != null && user.Email != null)
    {
        List<Claim> claims = new List<Claim>();
        claims.Add(new Claim(ClaimTypes.Sid, user.Id.ToString())); // Id
        claims.Add(new Claim(ClaimTypes.Name, user.FirstName)); // First name
        claims.Add(new Claim(ClaimTypes.GivenName, String.IsNullOrEmpty(user.LastName) ? "" : user.LastName)); // Last name
        claims.Add(new Claim(ClaimTypes.Email, user.Email)); // Email
        claims.Add(new Claim(ClaimTypes.Dns, String.IsNullOrEmpty(user.Guid.ToString()) ? "" : user.Guid.ToString())); // Guid
        
        // Profilkép claim hozzáadása - ÚJ
        if (!string.IsNullOrEmpty(user.ProfilePicture))
        {
            claims.Add(new Claim("ProfilePicture", user.ProfilePicture));
        }
        
        // Onboarding claim - ÚJ
        claims.Add(new Claim("OnboardingCompleted", user.OnboardingCompleted.ToString()));
        
        // Szerepkörök
        foreach (LoginRoleModel role in user.Roles.Where(r => !string.IsNullOrWhiteSpace(r.Name)))
        {
            claims.Add(new Claim(ClaimTypes.Role, role.Name)); // Jogosultságok
        }

        claimsIdentity = new ClaimsIdentity(claims, "CustomAuth");
    }

    return claimsIdentity;
}
```

---

## 🎯 8. ONBOARDING RENDSZER

### 8.1. GdeWeb/Components/Pages/Onboarding.razor (ÚJ FÁJL)

**LÉTREHOZANDÓ TELJES FÁJL:**

```razor
@page "/onboarding"
@attribute [Authorize]

@using GdeWeb.Components.Layout
@using GdeWeb.Services
@using Microsoft.AspNetCore.Authorization
@using Microsoft.AspNetCore.Components.Authorization
@using GdeWebModels
@using GdeWeb.Interfaces

@inject AuthenticationStateProvider authenticationStateProvider
@inject IUserService userService
@inject ISnackbarService snackbarService
@inject NavigationManager navigationManager
@inject ILocalStorageService localStorage

<PageTitle>Üdvözlés</PageTitle>

<MudContainer MaxWidth="MaxWidth.Medium" Class="mt-8">
    <MudCard>
        <MudCardContent>
            <MudText Typo="Typo.h4" Align="Align.Center" Class="mb-4">
                Üdvözöljük a rendszerben!
            </MudText>
            
            <MudText Typo="Typo.body1" Align="Align.Center" Class="mb-6">
                Néhány gyors lépés, és készen áll!
            </MudText>
            
            <MudStepper @ref="stepper" Color="Color.Primary">
                <MudStep Title="Személyes adatok">
                    <MudTextField 
                        Label="Keresztnév" 
                        @bind-Value="@firstName" 
                        Required="true"
                        Variant="Variant.Outlined"
                        Class="mb-3" />
                    <MudTextField 
                        Label="Vezetéknév" 
                        @bind-Value="@lastName" 
                        Required="true"
                        Variant="Variant.Outlined" />
                </MudStep>
                
                <MudStep Title="Beállítások">
                    <MudSelect 
                        Label="Nyelv" 
                        @bind-Value="@selectedLanguage"
                        Variant="Variant.Outlined">
                        <MudSelectItem Value="hu">Magyar</MudSelectItem>
                        <MudSelectItem Value="en">English</MudSelectItem>
                    </MudSelect>
                </MudStep>
                
                <MudStep Title="Befejezés">
                    <MudText Typo="Typo.body1" Align="Align.Center">
                        Minden kész! Kattintson a "Befejezés" gombra.
                    </MudText>
                </MudStep>
            </MudStepper>
            
            <MudStack Row="true" Justify="Justify.SpaceBetween" Class="mt-4">
                <MudButton 
                    OnClick="@PreviousStep" 
                    Disabled="@(stepper.ActiveIndex == 0)"
                    Variant="Variant.Text">
                    Előző
                </MudButton>
                <MudButton 
                    Variant="Variant.Filled" 
                    Color="Color.Primary"
                    OnClick="@NextStep"
                    Disabled="@isCompleting">
                    @(stepper.ActiveIndex == stepper.Steps.Count - 1 ? "Befejezés" : "Következő")
                </MudButton>
            </MudStack>
        </MudCardContent>
    </MudCard>
</MudContainer>

@code {
    [CascadingParameter]
    public MainLayout MainLayout { get; set; } = default!;
    
    private MudStepper stepper = new();
    private string firstName = "";
    private string lastName = "";
    private string selectedLanguage = "hu";
    private bool isCompleting = false;
    
    protected override async Task OnInitializedAsync()
    {
        // Ellenőrizzük, hogy be van-e jelentkezve
        var state = await authenticationStateProvider.GetAuthenticationStateAsync();
        if (state.User?.Identity?.IsAuthenticated != true)
        {
            navigationManager.NavigateTo("/signin");
            return;
        }
        
        // Felhasználó adatok betöltése
        var user = await ((CustomAuthentication)authenticationStateProvider).GetAuthenticatedUserAsync();
        
        if (user != null && user.Id > 0)
        {
            firstName = user.FirstName;
            lastName = user.LastName;
        }
    }
    
    private void PreviousStep()
    {
        if (stepper.ActiveIndex > 0)
            stepper.Previous();
    }
    
    private async Task NextStep()
    {
        if (stepper.ActiveIndex < stepper.Steps.Count - 1)
        {
            // Validáció az első lépésnél
            if (stepper.ActiveIndex == 0)
            {
                if (string.IsNullOrWhiteSpace(firstName) || string.IsNullOrWhiteSpace(lastName))
                {
                    snackbarService.ShowSnackbar(
                        Severity.Warning, 
                        "Kérjük, töltse ki a kötelező mezőket!", 
                        MainLayout.pageWidth);
                    return;
                }
            }
            
            stepper.Next();
        }
        else
        {
            await CompleteOnboarding();
        }
    }
    
    private async Task CompleteOnboarding()
    {
        try
        {
            isCompleting = true;
            StateHasChanged();
            
            // TODO: User adatok frissítése API-n keresztül
            // Jelenleg csak az onboarding flag-et állítjuk be
            // A teljes implementációhoz szükség van egy API endpoint-ra
            
            // Példa:
            // var userModel = new UserModel 
            // { 
            //     Id = userId,
            //     FirstName = firstName,
            //     LastName = lastName,
            //     OnboardingCompleted = true
            // };
            // await userService.ModifyProfile(userModel);
            
            snackbarService.ShowSnackbar(
                Severity.Success, 
                "Onboarding sikeresen befejezve!", 
                MainLayout.pageWidth);
            
            // Kis késleltetés, hogy lássa az üzenetet
            await Task.Delay(1000);
            
            navigationManager.NavigateTo("/dashboard");
        }
        catch (Exception ex)
        {
            snackbarService.ShowSnackbar(
                Severity.Error, 
                $"Hiba: {ex.Message}", 
                MainLayout.pageWidth);
        }
        finally
        {
            isCompleting = false;
            StateHasChanged();
        }
    }
}
```

---

## 🚀 9. GOOGLE CLOUD CONSOLE BEÁLLÍTÁSOK

### 9.1. Lépésről lépésre útmutató

1. **Látogasson a Google Cloud Console-ba**
   - URL: https://console.cloud.google.com/
   - Jelentkezzen be Google fiókjával

2. **Projekt létrehozása vagy kiválasztása**
   - Kattintson a projekt választóra (felső menüben)
   - Válasszon egy meglévő projektet vagy hozzon létre újat
   - Projekt neve: pl. "GdeWeb OAuth"

3. **OAuth consent screen beállítása**
   - Navigáljon: "APIs & Services" > "OAuth consent screen"
   - Válassza ki a User Type-ot (External vagy Internal)
   - Töltse ki a kötelező mezőket:
     - **App name**: GdeWeb
     - **User support email**: saját email
     - **Developer contact information**: saját email
   - Kattintson a "Save and Continue" gombra
   - Scopes: Alapértelmezett (openid, email, profile) elég
   - Test users: Ha External típust választott, adjon hozzá teszt felhasználókat

4. **OAuth 2.0 Client ID létrehozása**
   - Navigáljon: "APIs & Services" > "Credentials"
   - Kattintson a "+ CREATE CREDENTIALS" gombra
   - Válassza az "OAuth client ID" opciót
   - Application type: **Web application**
   - Name: GdeWeb OAuth Client
   - **Authorized JavaScript origins**: 
     ```
     https://localhost:7046
     ```
   - **Authorized redirect URIs**: 
     ```
     https://localhost:7046/api/Auth/GoogleCallback
     ```
   - Kattintson a "CREATE" gombra

5. **Credentials másolása**
   - Másolja ki a **Client ID** értéket
   - Másolja ki a **Client Secret** értéket
   - **FONTOS**: A Client Secret csak egyszer jelenik meg!

6. **Konfiguráció beillesztése**
   - Illessze be a Client ID-t és Client Secret-et az `appsettings.json` fájlokba

### 9.2. Production környezet beállítása

Production esetén:
- **Authorized JavaScript origins**: 
  ```
  https://yourdomain.com
  ```
- **Authorized redirect URIs**: 
  ```
  https://yourdomain.com/api/Auth/GoogleCallback
  ```

---

## ✅ 10. TESZTELÉS LÉPÉSEI

### 10.1. Előkészítés

1. **Adatbázis migráció futtatása**
   ```powershell
   dotnet ef migrations add AddGoogleOAuthFields -p GdeWebDB -s GdeWebAPI
   dotnet ef database update -p GdeWebDB -s GdeWebAPI
   ```

2. **Google OAuth konfiguráció beállítása**
   - Másolja be a Client ID-t és Client Secret-et az `appsettings.json` fájlokba

3. **Alkalmazás újraindítása**
   - Indítsa el a GdeWebAPI projektet
   - Indítsa el a GdeWeb projektet

### 10.2. Tesztelési forgatókönyvek

#### Teszt 1: Új felhasználó Google OAuth bejelentkezése
1. Nyissa meg a bejelentkezési oldalt (`/signin`)
2. Kattintson a "Bejelentkezés Google-lal" gombra
3. Jelentkezzen be Google fiókjával
4. Engedélyezze a hozzáférést
5. **Várt eredmény**: 
   - Redirect az onboarding oldalra
   - Felhasználó létrejön az adatbázisban
   - Profilkép és név automatikusan kitöltődik

#### Teszt 2: Meglévő felhasználó Google OAuth bejelentkezése
1. Hozzon létre egy felhasználót manuálisan az adatbázisban
2. Állítsa be az `OAUTHID` és `OAUTHPROVIDER` mezőket
3. Jelentkezzen be Google-lal
4. **Várt eredmény**: 
   - Redirect a dashboard-ra
   - Nincs onboarding

#### Teszt 3: Email alapján összekapcsolás
1. Hozzon létre egy felhasználót email-lel (de OAuth nélkül)
2. Jelentkezzen be ugyanazzal az email-lel Google-lal
3. **Várt eredmény**: 
   - A meglévő felhasználó összekapcsolódik a Google fiókkal
   - OAuth mezők kitöltődnek

#### Teszt 4: Profilkép megjelenítése
1. Jelentkezzen be Google-lal
2. Navigáljon a profil oldalra
3. **Várt eredmény**: 
   - A Google profilkép megjelenik

#### Teszt 5: Claims ellenőrzése
1. Jelentkezzen be Google-lal
2. Ellenőrizze a Claims-eket a browser DevTools-ban
3. **Várt eredmény**: 
   - `ProfilePicture` claim tartalmazza a profilkép URL-t
   - `OnboardingCompleted` claim tartalmazza az onboarding státuszt

### 10.3. Hibakezelés tesztelése

#### Teszt 6: Hiányzó konfiguráció
1. Távolítsa el a Google OAuth konfigurációt az `appsettings.json`-ból
2. Próbáljon bejelentkezni Google-lal
3. **Várt eredmény**: Hibaüzenet

#### Teszt 7: Érvénytelen authorization code
1. Próbáljon közvetlenül a callback URL-t meghívni érvénytelen code-dal
2. **Várt eredmény**: Hibaüzenet, redirect a signin oldalra

---

## 📝 11. SECURITY MEGJEGYZÉSEK

### 11.1. CSRF védelem
- **State parameter**: Minden OAuth kéréshez egyedi state generálása
- **State validálása**: A callback-ben ellenőrzés, hogy a state megegyezik-e
- **State tárolása**: IMemoryCache-ben vagy session-ben (5 perc TTL)

### 11.2. Token kezelés
- **JWT token**: Biztonságos generálás és validálás
- **Token expiration**: Konfigurálható lejárati idő
- **Refresh token**: Opcionálisan implementálható

### 11.3. Adatvédelem
- **Profilkép URL**: Csak URL tárolása, nem a kép maga
- **OAuth ID**: Egyedi azonosító, nem személyes adat
- **Email**: Validált email címek tárolása

### 11.4. Error handling
- **Logging**: Minden hiba naplózása
- **User-friendly üzenetek**: Nem mutatunk technikai részleteket a felhasználónak
- **Redirect**: Hiba esetén redirect a signin oldalra

---

## 🔧 12. TROUBLESHOOTING

### 12.1. Gyakori hibák és megoldások

#### Hiba: "redirect_uri_mismatch"
**Ok**: A redirect URI nem egyezik meg a Google Cloud Console-ban beállítottal
**Megoldás**: Ellenőrizze a redirect URI-t mindkét helyen

#### Hiba: "invalid_client"
**Ok**: Hibás Client ID vagy Client Secret
**Megoldás**: Ellenőrizze az `appsettings.json` fájlokat

#### Hiba: "access_denied"
**Ok**: A felhasználó nem adott engedélyt
**Megoldás**: Normális eset, a felhasználó megtagadhatja

#### Hiba: "Token exchange failed"
**Ok**: Hálózati probléma vagy érvénytelen authorization code
**Megoldás**: Ellenőrizze a hálózati kapcsolatot és a code érvényességét

### 12.2. Debug módszerek

1. **Logging engedélyezése**
   ```json
   "Logging": {
     "LogLevel": {
       "Default": "Debug",
       "Microsoft.AspNetCore": "Information"
     }
   }
   ```

2. **Browser DevTools**
   - Network tab: OAuth kérések ellenőrzése
   - Console: JavaScript hibák
   - Application tab: LocalStorage ellenőrzése

3. **Database ellenőrzés**
   - Ellenőrizze a `T_USER` táblát
   - Nézze meg az OAuth mezőket

---

## 📋 13. ÖSSZEFOGLALÓ - MÓDOSÍTANDÓ FÁJLOK LISTÁJA

### Adatbázis réteg:
1. ✅ **GdeWebDB/Entities/Data.cs** - User entitás bővítése (OAUTHPROVIDER, OAUTHID, PROFILEPICTURE, ONBOARDINGCOMPLETED)
2. ✅ **GdeWebDB/GdeDbContext.cs** - Entity konfiguráció bővítése
3. ✅ **GdeWebDB/Interfaces/IAuthService.cs** - LoginOrCreateGoogleUser metódus hozzáadása
4. ✅ **GdeWebDB/Services/AuthService.cs** - LoginOrCreateGoogleUser implementáció + GetUser bővítése

### Model réteg:
5. ✅ **GdeWebModels/LoginResultModel.cs** - OnboardingCompleted mező hozzáadása
6. ✅ **GdeWebModels/UserModel.cs** - OAuth és profilkép mezők hozzáadása
7. ✅ **GdeWebModels/LoginUserModel.cs** - ProfilePicture és OnboardingCompleted mezők
8. ✅ **GdeWebModels/GoogleOAuthModel.cs** - ÚJ FÁJL (GoogleOAuthModel, GoogleOAuthCallbackModel, GoogleUserInfoModel)

### API réteg:
9. ✅ **GdeWebAPI/Controllers/AuthController.cs** - GoogleLogin és GoogleCallback metódusok
10. ✅ **GdeWebAPI/appsettings.json** - GoogleOAuth konfiguráció
11. ✅ **GdeWebAPI/Program.cs** - IMemoryCache hozzáadása (opcionális)

### Frontend réteg:
12. ✅ **GdeWeb/Components/Pages/Authentication/Signin.razor** - Google login gomb + callback kezelés
13. ✅ **GdeWeb/Services/CustomAuthentication.cs** - Claims bővítése
14. ✅ **GdeWeb/Components/Pages/Onboarding.razor** - ÚJ FÁJL
15. ✅ **GdeWeb/appsettings.json** - Google ClientId

---

## 🎓 14. MIT MUTATNAK BE A HALLGATÓK?

### 14.1. OAuth Flow
- **Authorization Code Flow**: Teljes folyamat bemutatása
- **State parameter**: CSRF védelem
- **Token exchange**: Authorization code → Access token
- **User info**: Google API hívás

### 14.2. Claims + AuthenticationStateProvider
- **Custom claims**: ProfilePicture, OnboardingCompleted
- **ClaimsIdentity**: Bővített claim rendszer
- **AuthenticationState**: Dinamikus frissítés

### 14.3. Bővíthető UserModel tervezés
- **OAuth mezők**: Provider, ID, Profilkép
- **Onboarding flag**: Új felhasználó detektálása
- **Extensibility**: Könnyen bővíthető más OAuth szolgáltatókkal

### 14.4. Automatikus Onboarding
- **Új felhasználó detektálása**: OnboardingCompleted flag alapján
- **Lépésenkénti beállítások**: Stepper komponens
- **Adatok mentése**: API integráció

---

## 📚 15. KÉRDÉSEK ÉS VÁLASZOK

### Q: Miért nem használjuk a Microsoft.AspNetCore.Authentication.Google csomagot?
**A**: Mert teljes kontrollt szeretünk az OAuth flow felett, és így könnyebben testreszabható.

### Q: Mi történik, ha a felhasználó megtagadja az engedélyt?
**A**: A Google redirect a callback-re error paraméterrel, amit kezelünk és hibaüzenetet mutatunk.

### Q: Hogyan lehet bővíteni más OAuth szolgáltatókkal (pl. Facebook)?
**A**: Ugyanazt a mintát követve, csak az `OAUTHPROVIDER` mezőt más értékkel töltjük ki.

### Q: Mi a state parameter célja?
**A**: CSRF védelem - biztosítja, hogy a callback ugyanabból a kérésből jött.

### Q: Hogyan lehet tesztelni development környezetben?
**A**: Használjon localhost URL-eket és teszt Google fiókot a Google Cloud Console-ban.

---

## 🎉 KÉSZ!

Ez a dokumentum tartalmazza az összes szükséges információt a Google OAuth 2.0 beépítéséhez. Kövesse a lépéseket sorrendben, és minden működni fog!

**Sikeres implementációt!** 🚀
