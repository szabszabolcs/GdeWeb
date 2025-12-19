using GdeWebDB.Entities;
using GdeWebDB.Interfaces;
using GdeWebDB.Utilities;
using GdeWebModels;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Data.SqlClient;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static Microsoft.EntityFrameworkCore.DbLoggerCategory.Database;

namespace GdeWebDB.Services
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
                    .Where(ur => ur.Role != null && !string.IsNullOrEmpty(ur.Role.ROLENAME)) // árva/üres kizárása
                    .Select(ur => new LoginRoleModel
                    {
                        Id = ur.Role.ROLEID,
                        Name = ur.Role.ROLENAME
                    })
                    .Distinct() // ha lenne duplikált USERID–ROLEID
                    .ToListAsync();

                // 3) Összerakjuk a LoginResultModel-t
                return new LoginResultModel
                {
                    Id = user.USERID,
                    Guid = user.GUID,
                    Active = user.ACTIVE,
                    Roles = roles,
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

        // Az aktív mezőt nem vizsgálja -> Regisztrációs folyamatnál kell,
        // mielőtt a confirmation levél kimegy (UserController -> AddUser)
        public async Task<LoginResultModel> Auth(LoginModel credentials)
        {
            try
            {
                var user = await _db.T_USER
                    .AsNoTracking()
                    .FirstOrDefaultAsync(u =>
                        u.EMAIL == credentials.Email &&
                        u.PASSWORD == credentials.Password);

                if (user == null)
                {
                    return new LoginResultModel
                    {
                        Result = new ResultModel
                        {
                            Success = false,
                            ErrorMessage = "Invalid username or password provided!"
                        }
                    };
                }

                var roles = await _db.K_USER_ROLES
                    .Where(ur => ur.USERID == user.USERID)
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
                    Id = user.USERID,
                    Guid = user.GUID,
                    Active = user.ACTIVE, // csak visszaadjuk, nem ellenőrizzük
                    Roles = roles,
                    Result = new ResultModel { Success = true }
                };
            }
            catch (Exception ex)
            {
                await _logService.WriteLogToFile(ex, "Auth hiba");
                return new LoginResultModel { Result = ResultTypes.UnexpectedError };
            }
        }

        // Csak az email-t vizsgálja -> Elfelejtett jelszónál kell,
        // mielőtt a forgot password levél kimegy (UserController -> ForgotPassword)
        public async Task<LoginResultModel> Forgot(ForgotModel model)
        {
            try
            {
                var user = await _db.T_USER
                    .AsNoTracking()
                    .FirstOrDefaultAsync(u => u.EMAIL == model.Email);

                if (user == null)
                {
                    return new LoginResultModel
                    {
                        Result = new ResultModel
                        {
                            Success = false,
                            ErrorMessage = "The provided email address does not exist!"
                        }
                    };
                }

                var roles = await _db.K_USER_ROLES
                    .Where(ur => ur.USERID == user.USERID)
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
                    Id = user.USERID,
                    Guid = user.GUID,
                    Active = user.ACTIVE,
                    Roles = roles,
                    Result = new ResultModel { Success = true }
                };
            }
            catch (Exception ex)
            {
                await _logService.WriteLogToFile(ex, "Forgot hiba");
                return new LoginResultModel { Result = ResultTypes.UnexpectedError };
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

        public async Task<ResultModel> AddUserTokenExpirationDate(int userId, string token, DateTime expirationDate)
        {
            try
            {
                var entity = new AuthToken
                {
                    USERID = userId,
                    TOKEN = token,
                    EXPIRATIONDATE = expirationDate
                };

                _db.T_AUTHENTICATION.Add(entity);
                await _db.SaveChangesAsync();

                return new ResultModel { Success = true };
            }
            catch (Exception ex)
            {
                await _logService.WriteLogToFile(ex, "AddUserTokenExpirationDate hiba");
                return ResultTypes.UnexpectedError;
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
                        Email = u.EMAIL ?? String.Empty,
                        ProfilePicture = u.PROFILEPICTURE ?? String.Empty,
                        OnboardingCompleted = u.ONBOARDINGCOMPLETED,
                        UserDataJson = u.USERDATAJSON
                    })
                    .FirstOrDefaultAsync();

                if (user == null)
                    return new LoginUserModel { Result = ResultTypes.NotFound };

                // 2) Szerepkörök (INNER JOIN K_USER_ROLES → T_ROLE)
                user.Roles = await _db.K_USER_ROLES
                    .Where(ur => ur.USERID == userId)
                    .Where(ur => ur.Role != null && !string.IsNullOrEmpty(ur.Role.ROLENAME)) // árva/üres kizárása
                    .Select(ur => new LoginRoleModel
                    {
                        Id = ur.Role.ROLEID,
                        Name = ur.Role.ROLENAME
                    })
                    .Distinct() // ha lenne duplikált USERID–ROLEID
                    .ToListAsync();

                // 3) legfrissebb nem lejárt token beolvasása és beállítása
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
                // használd a saját log szolgáltatásod
                await _logService.WriteLogToFile(ex, "Login hiba");
                return new LoginUserModel { Result = ResultTypes.UnexpectedError }; // Megjegyzés
            }
        }

        public async Task<ResultModel> UserValidation(int userId, string userGuid)
        {
            try
            {
                // Ha a GUID az entitásban Guid típus, a userGuid-ot parse-oljuk:
                // (Ha string, az EF simán lefordítja string összehasonlításra.)
                bool isValid;
                try
                {
                    // Két lehetséges mappinghez kompatibilis megoldás:
                    isValid = await _db.T_USER.AnyAsync(u =>
                        u.USERID == userId &&
                        (u.GUID.ToString() == userGuid || u.GUID.ToString() == userGuid));
                }
                catch
                {
                    // Ha az előbbi nem fordítható SQL-re (ritka eset), egyszerűsítünk:
                    isValid = await _db.T_USER.AnyAsync(u => u.USERID == userId && u.GUID.ToString() == userGuid);
                }

                if (isValid)
                {
                    return new ResultModel { Success = true, ErrorMessage = "" };
                }
                else
                {
                    return new ResultModel { Success = false, ErrorMessage = "User is not valid!" };
                }
            }
            catch (Exception ex)
            {
                await _logService.WriteLogToFile(ex, "UserValidation hiba");
                return ResultTypes.UnexpectedError;
            }
        }

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
                    
                    //// Ha nincs jelszó, generálunk egyet (OAuth esetén opcionális)
                    //if (string.IsNullOrEmpty(userByEmail.PASSWORD))
                    //{
                    //    userByEmail.PASSWORD = Utilities.Utilities.EncryptPassword(Guid.NewGuid().ToString());
                    //}
                    
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
                    //PASSWORD = Utilities.Utilities.EncryptPassword(Guid.NewGuid().ToString()), // Random jelszó
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
    }
}