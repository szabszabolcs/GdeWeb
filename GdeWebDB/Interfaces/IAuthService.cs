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

        // Google OAuth login vagy létrehozás
        Task<LoginResultModel> LoginOrCreateGoogleUser(System.Text.Json.JsonElement googleUserInfo);
    }
}
