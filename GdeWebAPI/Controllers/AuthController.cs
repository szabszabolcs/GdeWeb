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
using System.Text.Json;

namespace GdeWebAPI.Controllers
{
    /// <summary>
    /// Felhasználók hitelesítéséért és jogosultsági tokenek kezeléséért felelős API vezérlő.
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    [DisableRateLimiting] // Az egész controllerre érvényes, hogy nincs Rate Limiting
    public class AuthController : ControllerBase
    {
        private readonly IConfiguration _configuration;

        private readonly IAuthService _authService;

        private readonly ILogService _logService;

        /// <summary>
        /// Létrehozza az <see cref="AuthController"/> példányt a szükséges szolgáltatásokkal.
        /// </summary>
        /// <param name="configuration">Az alkalmazás konfigurációs beállításai.</param>
        /// <param name="authService">A felhasználók hitelesítéséért felelős szolgáltatás.</param>
        /// <param name="logService">A naplózási műveletekért felelős szolgáltatás.</param>
        public AuthController(IConfiguration configuration, IAuthService authService, ILogService logService)
        {
            ArgumentNullException.ThrowIfNull(configuration);
            ArgumentNullException.ThrowIfNull(authService);
            ArgumentNullException.ThrowIfNull(logService);

            this._configuration = configuration;
            this._authService = authService;
            this._logService = logService;
        }

        /// <summary>
        /// Felhasználó bejelentkeztetése. Sikeres hitelesítés esetén JWT tokent ad vissza.
        /// </summary>
        /// <param name="credential">A bejelentkezéshez szükséges adatok (felhasználónév, jelszó).</param>
        /// <returns>
        /// 200 OK – érvényes tokennel, ha a bejelentkezés sikeres.  
        /// 401 Unauthorized – ha a hitelesítés sikertelen.
        /// </returns>
        [HttpPost]
        [Route("Login")]
        [ApiExplorerSettings(IgnoreApi = true)] // [ApiExplorerSettings(IgnoreApi = true)]
        [SwaggerOperation(
            Summary = "Bejelentkezés felhasználónévvel és jelszóval",
            Description = "LoginResultModel = Login(LoginModel credentials)"
        )]
        [Consumes(MediaTypeNames.Application.Json)] // "application/json"
        [Produces("application/json")]
        public async Task <LoginResultModel> Login([FromBody] LoginModel credential)
        {
            // SHA - 512 jelszó hashelés
            //LoginModel encryptedCredentials = new LoginModel
            //{
            //    Email = credential.Email,
            //    Password = Utilities.Utilities.EncryptPassword(credential.Password)
            //};

            LoginResultModel loginResult = await _authService.Login(credential);

            if (loginResult.Result.Success)
            {
                // Token generálás
                string token = Utilities.Utilities.GenerateToken(loginResult, _configuration);

                loginResult.Token = token;

                double time = Convert.ToDouble(_configuration["Jwt:ExpireMinutes"]);

                ResultModel resultModel = await _authService.AddUserTokenExpirationDate(loginResult.Id, token, DateTime.Now.AddHours(time));
            }

            return loginResult;
        }

        /// <summary>
        /// Visszaadja a felhasználó adatait egy meglévő hitelesítési token alapján.
        /// </summary>
        /// <param name="token">A token értékét tartalmazó modell.</param>
        /// <returns>
        /// 200 OK – ha a token érvényes és a felhasználó azonosítható.  
        /// 401 Unauthorized – ha a token érvénytelen vagy lejárt.
        /// </returns>
        [HttpPost]
        [Route("GetUserFromToken")]
        [ApiExplorerSettings(IgnoreApi = true)]
        [SwaggerOperation(
            Summary = "Bejelentkezés tokennel",
            Description = "LoginUserModel = GetUserFromToken(LoginTokenModel token)"
        )]
        [Consumes(MediaTypeNames.Application.Json)] // "application/json"
        [Produces("application/json")]
        public async Task<LoginUserModel> GetUserFromToken([FromBody] LoginTokenModel token)
        {
            int userId = Utilities.Utilities.GetUserIdFromToken(token.Token);

            if (userId == -1)
            {
                return new LoginUserModel() { Result = ResultTypes.UserAuthenticateError };
            }

            // CHECK EXPIRATION DATE
            double time = Convert.ToDouble(_configuration["Jwt:ExpireInHours"]);
            ResultModel result = await _authService.GetUserTokenExpirationDate(userId, DateTime.Now.AddHours(time));
            if (!result.Success)
            {
                return new LoginUserModel() { Result = ResultTypes.UserAuthenticateError };
            }

            LoginUserModel user = await _authService.GetUser(userId);
            user.Token = token.Token; // Visszaadja a tokent is

            return user;
        }

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
                
                // Google OAuth URL összeállítása
                var googleAuthUrl = $"https://accounts.google.com/o/oauth2/v2/auth?" +
                    $"client_id={Uri.EscapeDataString(clientId)}&" +
                    $"redirect_uri={Uri.EscapeDataString(redirectUri)}&" +
                    $"response_type=code&" +
                    $"scope=openid%20email%20profile&" +
                    $"state={Uri.EscapeDataString(state)}&" +
                    $"access_type=offline&" +
                    $"prompt=consent";
                
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
                var loginResult = await _authService.LoginOrCreateGoogleUser(googleUserInfo.Value);
                
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
