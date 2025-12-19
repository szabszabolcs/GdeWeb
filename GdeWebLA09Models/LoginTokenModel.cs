using Swashbuckle.AspNetCore.Annotations;

namespace GdeWebLA09Models
{
    [SwaggerSchema("Login token osztálya")]
    public class LoginTokenModel
    {
        [SwaggerSchema("Login token azonosítója")]
        public string Token { get; set; } = String.Empty;
    }
}