
using GdeWebAPI.Middleware;
using GdeWebAPI.Services;
using GdeWebDB;
using GdeWebDB.Interfaces;
using GdeWebDB.Services;
using LangChain.Providers;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using Microsoft.OpenApi.Models;
using System.Reflection;
using System.Threading.RateLimiting;

namespace GdeWebAPI
{
    /// <summary>
    /// Alkalmazás belépési pontja és host konfiguráció.
    /// </summary>
    public class Program
    {
        /// <summary>
        /// Az alkalmazás belépési pontja. Beállítja a hostot, a szolgáltatásokat és elindítja a webalkalmazást.
        /// </summary>
        /// <param name="args">Parancssori argumentumok.</param>
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // Cors policy
            var myAllowSpecificOrigins = "_myPolicy";
            builder.Services.AddCors(options =>
            {
                options.AddPolicy(name: myAllowSpecificOrigins,
                                  policy =>
                                  {
                                      policy.WithOrigins("http://localhost",
                                                          "https://eduai.omegacode.cloud")
                                      .WithMethods("POST", "PUT", "DELETE", "GET")
                                      .SetIsOriginAllowedToAllowWildcardSubdomains()
                                      .AllowAnyOrigin()
                                      .AllowAnyHeader()
                                      .AllowAnyMethod();
                                  });
            });

            // Message Rate Limiter
            builder.Services.AddRateLimiter(options =>
            {
                options.AddFixedWindowLimiter("MessagePolicy", opt =>
                {
                    opt.PermitLimit = 1;              // 1 kérés
                    opt.Window = TimeSpan.FromMinutes(1); // percenként
                    opt.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
                    opt.QueueLimit = 0;               // ne soroljunk
                });

                // 💬 Egyedi hibaüzenet, ha a limit túllépésre kerül
                options.OnRejected = async (context, token) =>
                {
                    context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests; // vagy 503, ha úgy tetszik
                    context.HttpContext.Response.ContentType = "application/json; charset=utf-8";

                    var message = new
                    {
                        success = false,
                        error = "Az ön hitelesítő tokenje korlátozva van: percenként legfeljebb 1 kérés engedélyezett az ön szerveréről."
                    };

                    var json = System.Text.Json.JsonSerializer.Serialize(message);
                    await context.HttpContext.Response.WriteAsync(json, token);
                };
            });

            // SQLite file az app mappájában
            var cs = builder.Configuration.GetConnectionString("Sqlite") ?? "Data Source=./gde.db";

            builder.Services.AddDbContext<GdeDbContext>(opt => opt.UseSqlite(cs));

            // AI SERVICE
            builder.Services.AddHttpClient("openai", c =>
            {
                c.Timeout = TimeSpan.FromMinutes(10); // vagy Timeout.InfiniteTimeSpan
                //c.Timeout = Timeout.InfiniteTimeSpan;
                // Accept header-t a kérésnél is állítjuk majd a stream-re, de itt is maradhat általános
            });
            builder.Services.AddSingleton<AiService>();   // saját, pici szolgáltatás

            builder.Services.AddScoped<IAuthService, AuthService>(); // az új EF-es AuthService
            builder.Services.AddScoped<IUserService, UserService>();
            builder.Services.AddScoped<ITrainingService, TrainingService>();

            // Ha a MailService / LogService is DbContextet használ, azokat is Scoped-ra.
            // Ha nem használ DbContextet és stateless, maradhat Singleton.
            builder.Services.AddSingleton<IMailService, MailService>();
            builder.Services.AddSingleton<ILogService, LogService>();
            

            // Hosted Service - Background Service regisztráció
            builder.Services.AddHostedService<HostedService>();

            // Add services to the container.

            builder.Services.AddControllersWithViews();
            // Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
            
            // Middleware szolgáltatások regisztrációja
            builder.Services.AddScoped<Middleware.AccessTokenFilter>();
            //builder.Services.AddControllers(o => o.Filters.Add<AccessTokenFilter>());

            builder.Services.AddEndpointsApiExplorer();
            builder.Services.AddSwaggerGen(c =>
            {
                c.EnableAnnotations();
                c.SwaggerDoc("v1", new OpenApiInfo
                {
                    Title = "Gde.API",
                    Version = "v1",
                    Description = "Gde1.API Swagger Documentation",
                    Contact = new OpenApiContact
                    {
                        Name = "Gde Developer",
                        Email = "teszt@gde.hu",
                    },
                    License = new OpenApiLicense
                    {
                        Name = "MIT License",
                        Url = new Uri("https://opensource.org/licenses/MIT")
                    }
                });

                // Set the comments path for the Swagger JSON and UI.**
                var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
                var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
                c.IncludeXmlComments(xmlPath);
            });

            var app = builder.Build();

            // Migrációk automatikus alkalmazása indításkor
            using (var scope = app.Services.CreateScope())
            {
                var dbContext = scope.ServiceProvider.GetRequiredService<GdeDbContext>();
                try
                {
                    dbContext.Database.Migrate();
                }
                catch (Exception ex)
                {
                    // Ha a migráció sikertelen, próbáljuk meg manuálisan hozzáadni az oszlopokat
                    try
                    {
                        dbContext.Database.ExecuteSqlRaw("ALTER TABLE T_USER ADD COLUMN OAUTHPROVIDER TEXT NULL;");
                    }
                    catch { }
                    try
                    {
                        dbContext.Database.ExecuteSqlRaw("ALTER TABLE T_USER ADD COLUMN OAUTHID TEXT NULL;");
                    }
                    catch { }
                    try
                    {
                        dbContext.Database.ExecuteSqlRaw("ALTER TABLE T_USER ADD COLUMN PROFILEPICTURE TEXT NULL;");
                    }
                    catch { }
                    try
                    {
                        dbContext.Database.ExecuteSqlRaw("ALTER TABLE T_USER ADD COLUMN ONBOARDINGCOMPLETED INTEGER NOT NULL DEFAULT 0;");
                    }
                    catch { }
                }
            }

            // Configure the HTTP request pipeline.
            if (app.Environment.IsDevelopment() || app.Environment.IsProduction())
            {
                app.UseSwagger();
                app.UseSwaggerUI(c =>
                {
                    c.SwaggerEndpoint("/swagger/v1/swagger.json", "Gde1.API v1");
                    //c.RoutePrefix = string.Empty; // Set Swagger UI at the app's root
                    c.EnableDeepLinking();
                });
            }

            app.UseHttpsRedirection();

            // >>> statikus fájlok kiszolgálása
            var provider = new FileExtensionContentTypeProvider();
            provider.Mappings[".mp4"] = "video/mp4";   // biztos, ami biztos

            app.UseStaticFiles(new StaticFileOptions
            {
                ContentTypeProvider = provider
                // RequestPath nélkül a wwwroot gyökerét szolgáljuk ki
            });

            app.UseCors(myAllowSpecificOrigins);

            app.UseAuthorization();

            // Rate Limiter
            app.UseRateLimiter();

            app.MapControllers();

            app.Run();
        }
    }
}
