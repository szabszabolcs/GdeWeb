using GdeWebLA09Models;

namespace GdeWebLA09.Interfaces
{
    public interface IWeatherService
    {
        Task<List<WeatherForecast>> GetWeatherForecastsAsync();
    }
}
