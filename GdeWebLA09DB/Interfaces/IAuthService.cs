using GdeWebLA09Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GdeWebLA09DB.Interfaces
{
    public interface IAuthService
    {
        Task<LoginResultModel> Login(LoginModel credential);

        Task<ResultModel> GetUserTokenExpirationDate(int userId, DateTime expirationDate);

        Task<LoginUserModel> GetUser(int userId);
    }
}
