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

