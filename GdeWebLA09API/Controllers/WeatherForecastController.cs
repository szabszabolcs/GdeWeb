using GdeWebLA09API.Middleware;
using GdeWebLA09DB.Interfaces;
using GdeWebLA09Models;
using Microsoft.AspNetCore.Mvc;
using System.Net.Mime;

namespace GdeWebLA09API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class WeatherForecastController : ControllerBase
    {
        private static readonly string[] Summaries = new[]
        {
            "Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching"
        };

        private readonly ILogger<WeatherForecastController> _logger;

        private readonly ILogService _logService;

        public WeatherForecastController(ILogger<WeatherForecastController> logger, ILogService logService)
        {
            _logger = logger;
            this._logService = logService;
        }

        [HttpGet]
        [Route("GetWeatherForecast")]
        [Consumes(MediaTypeNames.Application.Json)]
        [Produces("application/json")]
        [ServiceFilter(typeof(AccessTokenFilter))]
        public async Task<List<WeatherForecast>> GetWeatherForecast()
        {
            if (HttpContext.Request.Headers.TryGetValue("AccessToken", out var accessTokenHeader))
            {
                if (accessTokenHeader != "gd3t0k3n")
                {
                    HttpContext.Response.StatusCode = 403; // Forbidden
                    return new List<WeatherForecast>();
                }

                _logService.WriteMessageLogToFile("AccessToken érvényesítve", "WeatherForecast lekérés");

                return Enumerable.Range(1, 5).Select(index => new WeatherForecast
                {
                    Date = DateOnly.FromDateTime(DateTime.Now.AddDays(index)),
                    TemperatureC = Random.Shared.Next(-20, 55),
                    Summary = Summaries[Random.Shared.Next(Summaries.Length)]
                })
                .ToList();
            }
            else
            {
                HttpContext.Response.StatusCode = 403; // Forbidden
                return new List<WeatherForecast>();
            }
        }
    }
}
