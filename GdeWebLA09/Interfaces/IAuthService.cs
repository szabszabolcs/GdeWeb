using GdeWebLA09Models;

namespace GdeWebLA09.Interfaces
{
    public interface IAuthService
    {
        Task<LoginResultModel> Login(LoginModel credential);

        Task<LoginUserModel> GetUserFromToken(LoginTokenModel credential);
    }
}
