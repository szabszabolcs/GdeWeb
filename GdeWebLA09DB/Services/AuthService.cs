using GdeWebLA09DB.Entities;
using GdeWebLA09DB.Interfaces;
using GdeWebLA09DB.Utilities;
using GdeWebLA09Models;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Data.SqlClient;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static Microsoft.EntityFrameworkCore.DbLoggerCategory.Database;

namespace GdeWebLA09DB.Services
{
    public class AuthService : IAuthService
    {
        private readonly GdeDbContext _db;

        private readonly ILogService _logService;

        public AuthService(GdeDbContext db, ILogService logService)
        {
            _db = db;
            _logService = logService;
        }

        public async Task<LoginResultModel> Login(LoginModel credential)
        {
            try
            {
                
                //var passHash = PasswordHasher.Sha512(credential.Password);
                Console.WriteLine(credential.Password);
                if (credential.Password == "3774b62586f7e44b343a0f2cc8e0e336c0d03549e09162cb73dfcb9a72c15a95c40f5a5b1608a08a28a79f46450ad3391f0a9342a19fdebb56511b556ba6aabb")
                {
                    Console.WriteLine("Logged");
                }

                // 1) User lekérdezése email + hash alapján
                var user = await _db.T_USER
                    .AsNoTracking()
                    .FirstOrDefaultAsync(u => u.EMAIL == credential.Email && u.PASSWORD == credential.Password);

                if (user == null)
                {
                    return new LoginResultModel
                    {
                        Result = new ResultModel
                        {
                            Success = false,
                            ErrorMessage = "Hibás e-mail vagy jelszó."
                        }
                    };
                }

                if (!user.ACTIVE)
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

                // 2) Szerepek (lehet több is)
                var roles = await _db.K_USER_ROLES
                    .Where(ur => ur.USERID == user.USERID)
                    .Include(ur => ur.Role)
                    .Select(ur => new LoginRoleModel
                    {
                        Id = ur.Role.ROLEID,
                        Name = ur.Role.ROLENAME
                    })
                    .ToListAsync();

                // 3) Összerakjuk a LoginResultModel-t
                return new LoginResultModel
                {
                    Id = user.USERID,
                    Guid = user.GUID,
                    Active = user.ACTIVE,
                    Roles = roles, // ha a modelled stringet vár, módosítsd List<string>-re
                    Result = new ResultModel
                    {
                        Success = true,
                        ErrorMessage = string.Empty
                    }
                };
            }
            catch (Exception ex)
            {
                await _logService.WriteLogToFile(ex, "Login hiba");
                throw;
            }
        }

        public async Task<ResultModel> GetUserTokenExpirationDate(int userId, DateTime expirationDate)
        {
            // A régi logika EF-fel:
            // - ha van még nem lejárt token: a legkésőbbi lejáratút frissítjük, a többit töröljük
            // - ha nincs: töröljük az összes token-t és Result=false
            try
            {
                using var tx = await _db.Database.BeginTransactionAsync();

                var now = DateTime.UtcNow;

                var validTokens = await _db.T_AUTHENTICATION
                    .Where(t => t.USERID == userId && t.EXPIRATIONDATE > now)
                    .OrderByDescending(t => t.EXPIRATIONDATE)
                    .ToListAsync();

                if (validTokens.Count == 0)
                {
                    // nincs érvényes – mindent törlünk az adott userhez
                    var allUserTokens = _db.T_AUTHENTICATION.Where(t => t.USERID == userId);
                    _db.T_AUTHENTICATION.RemoveRange(allUserTokens);
                    await _db.SaveChangesAsync();
                    await tx.CommitAsync();

                    return new ResultModel
                    {
                        Success = false,
                        ErrorMessage = "The provided token does not exist or has expired!"
                    };
                }
                else
                {
                    var top = validTokens.First();

                    // friss lejárati dátum
                    top.EXPIRATIONDATE = expirationDate;
                    _db.T_AUTHENTICATION.Update(top);

                    // a többit töröljük
                    var others = validTokens.Skip(1);
                    _db.T_AUTHENTICATION.RemoveRange(others);

                    await _db.SaveChangesAsync();
                    await tx.CommitAsync();

                    return new ResultModel { Success = true };
                }
            }
            catch (Exception ex)
            {
                await _logService.WriteLogToFile(ex, "Login hiba");
                throw;
            }
        }

        public async Task<LoginUserModel> GetUser(int userId)
        {
            try
            {
                // 1) User adatok
                var user = await _db.T_USER
                    .AsNoTracking()
                    .Where(u => u.USERID == userId)
                    .Select(u => new LoginUserModel
                    {
                        Id = u.USERID,
                        // Ha a LoginUserModel.Guid típusa string, akkor: Guid = u.GUID.ToString()
                        Guid = u.GUID,
                        FirstName = u.FIRSTNAME,
                        LastName = u.LASTNAME,
                        Email = u.EMAIL
                    })
                    .FirstOrDefaultAsync();

                if (user == null)
                    return new LoginUserModel { Result = ResultTypes.NotFound };

                // 2) Szerepkörök (INNER JOIN K_USER_ROLES → T_ROLE)
                user.Roles = await _db.K_USER_ROLES
                    .Where(k => k.USERID == userId)
                    .Include(k => k.Role)
                    .Select(k => new LoginRoleModel
                    {
                        Id = k.Role.ROLEID,
                        Name = k.Role.ROLENAME
                    })
                    .ToListAsync();

                user.Result = new ResultModel { Success = true };
                return user;
            }
            catch (Exception ex)
            {
                // használd a saját log szolgáltatásod
                await _logService.WriteLogToFile(ex, "Login hiba");
                return new LoginUserModel { Result = ResultTypes.UnexpectedError }; // Megjegyzés
            }
        }
    }
}

