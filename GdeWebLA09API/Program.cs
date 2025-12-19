
using GdeWebLA09API.Middleware;
using GdeWebLA09DB;
using GdeWebLA09DB.Interfaces;
using GdeWebLA09DB.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;
using System.Reflection;

namespace GdeWebLA09API
{
    public class Program
    {
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
                                      policy.WithOrigins("https://localhost:7118",
                                                          "http://localhost")
                                      .WithMethods("POST", "PUT", "DELETE", "GET")
                                      .SetIsOriginAllowedToAllowWildcardSubdomains()
                                      .AllowAnyHeader()
                                      .AllowAnyMethod();
                                  });
            });

            // SQLite file az app mappájában
            var cs = builder.Configuration.GetConnectionString("Sqlite") ?? "Data Source=./gde.db";

            builder.Services.AddDbContext<GdeDbContext>(opt => opt.UseSqlite(cs));

            builder.Services.AddScoped<IAuthService, AuthService>(); // az új EF-es AuthService
            builder.Services.AddScoped<ILogService, LogService>();

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
                    Title = "Gde1.API",
                    Version = "v1",
                    Description = "Gde1.API Swagger Documentation",
                    Contact = new OpenApiContact
                    {
                        Name = "Gde1 Developer",
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

            app.UseCors(myAllowSpecificOrigins);

            //app.UseMiddleware<AccessTokenFilter>();

            app.UseAuthorization();


            app.MapControllers();

            app.Run();
        }
    }
}
