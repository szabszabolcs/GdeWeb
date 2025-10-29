using Blazored.LocalStorage;
using GdeWebLA09.Components;
using GdeWebLA09.Interfaces;
using GdeWebLA09.Services;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components.Authorization;

namespace GdeWebLA09
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // Read appsettings.json apiUrl
            var configuration = new ConfigurationBuilder()
                .SetBasePath(builder.Environment.ContentRootPath)
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                .Build();
            var apiUrl = configuration.GetValue<string>("apiUrl");

            // Add services to the container.
            builder.Services.AddRazorComponents()
                .AddInteractiveServerComponents();

            // Local storage szolgáltatás hozzáadása
            builder.Services.AddBlazoredLocalStorage();

            // Szolgáltatások hozzáadása
            builder.Services.AddRazorPages();
            builder.Services.AddServerSideBlazor().AddCircuitOptions(options => { options.DetailedErrors = true; });

            // Service registrations
            builder.Services.AddHttpClient<IWeatherService, WeatherService>(c => c.BaseAddress = new Uri(apiUrl));
            builder.Services.AddHttpClient<IAuthService, AuthService>(c => c.BaseAddress = new Uri(apiUrl));


            // 1) Hitelesítés (cookie példa)
            // Authentication Core - For authentication
            builder.Services.AddAuthentication();

            // 2) Jogosultságkezelés
            // ⚠️ NINCS Cookie/Bearer auth itt
            builder.Services.AddAuthorization();                     // kell az [Authorize]-hoz

            // Cascading Authentication
            builder.Services.AddCascadingAuthenticationState(); // kell az AuthorizeView-hoz

            // Authentication Service
            builder.Services.AddScoped<AuthenticationStateProvider, CustomAuthentication>();

            builder.Services.AddHttpClient<CustomAuthentication>(client =>
            {
                client.BaseAddress = new Uri(apiUrl);
            });


            var app = builder.Build();

            // Configure the HTTP request pipeline.
            if (!app.Environment.IsDevelopment())
            {
                app.UseExceptionHandler("/Error");
                // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
                app.UseHsts();
            }

            app.UseHttpsRedirection();

            // Middleware-k
            app.UseStaticFiles();

            app.UseRouting(); // Hozzáadjuk a routingot
            app.UseAntiforgery(); // Hozzáadjuk az antiforgery middleware-t
            app.MapControllers(); // Térképezzük fel a kontrollereket

            app.MapRazorComponents<App>()
                .AddInteractiveServerRenderMode();

            // Refresh
            app.MapFallbackToFile("/");

            app.Run();
        }
    }
}
