using GdeWebLA09DB.Interfaces;
using GdeWebLA09DB.Services;
using GdeWebLA09DB.Utilities;
using GdeWebLA09Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using Swashbuckle.AspNetCore.Annotations;
using System.Data;
using System.IdentityModel.Tokens.Jwt;
using System.Net.Mime;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

namespace GdeWebLA09API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AuthController : ControllerBase
    {
        private readonly IConfiguration _configuration;

        private readonly IAuthService _authService;

        private readonly ILogService _logService;

        public AuthController(IConfiguration configuration, IAuthService authService, ILogService logService)
        {
            this._configuration = configuration;
            this._authService = authService;
            this._logService = logService;
        }

        [HttpPost]
        [Route("Login")]
        [ApiExplorerSettings(IgnoreApi = false)] // [ApiExplorerSettings(IgnoreApi = true)]
        [SwaggerOperation(
            Summary = "Bejelentkezés felhasználónévvel és jelszóval",
            Description = "LoginResultModel = Login(LoginModel credentials)"
        )]
        [Consumes(MediaTypeNames.Application.Json)] // "application/json"
        [Produces("application/json")]
        public async Task <LoginResultModel> Login(LoginModel credential)
        {
            // SHA - 512 jelszó hashelés
            LoginModel encryptedCredentials = new LoginModel
            {
                Email = credential.Email,
                Password = Utilities.Utilities.EncryptPassword(credential.Password)
            };

            LoginResultModel loginResult = await _authService.Login(encryptedCredentials);

            if (loginResult.Result.Success)
            {
                // Token generálás
                string token = Utilities.Utilities.GenerateToken(loginResult, _configuration);

                loginResult.Token = token;

                double time = Convert.ToDouble(_configuration["Jwt:ExpireMinutes"]);

                ResultModel resultModel = await _authService.GetUserTokenExpirationDate(loginResult.Id, DateTime.Now.AddHours(time));
            }

            return loginResult;
        }

        [HttpPost]
        [Route("GetUserFromToken")]
        [ApiExplorerSettings(IgnoreApi = true)]
        [SwaggerOperation(
            Summary = "Bejelentkezés tokennel",
            Description = "LoginUserModel = GetUserFromToken(LoginTokenModel token)"
        )]
        [Consumes(MediaTypeNames.Application.Json)] // "application/json"
        [Produces("application/json")]
        public async Task<LoginUserModel> GetUserFromToken(LoginTokenModel token)
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
    }      
}
