using Swashbuckle.AspNetCore.Annotations;

namespace GdeWebLA09Models
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

        [SwaggerSchema("Login felhasználó szerepkör listája")]
        public List<LoginRoleModel> Roles { get; set; } = new List<LoginRoleModel> { new LoginRoleModel() };

        [SwaggerSchema("Művelet sikeressége")]
        public ResultModel Result { get; set; } = new ResultModel();
    }
}