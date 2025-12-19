using Blazored.LocalStorage;
using GdeWebLA09.Interfaces;
using GdeWebLA09Models;
using Newtonsoft.Json;
using System.Net.Http.Headers;

namespace GdeWebLA09.Services
{
    public class WeatherService : IWeatherService
    {
        private readonly HttpClient _httpClient;

        private readonly ILocalStorageService _localStorageService;

        public WeatherService(HttpClient httpClient, ILocalStorageService localStorageService)
        {
            _httpClient = httpClient;
            _localStorageService = localStorageService;

            // ideiglenes token
            localStorageService.SetItemAsync("AccessToken", "gd3t0k3n");
        }



        private async Task<T> SendGetRequest<T>(string endpoint, bool requireAuth = true)
        {
            HttpRequestMessage request;
            if (requireAuth)
            {
                var accessToken = await _localStorageService.GetItemAsync<string>("AccessToken");
                if (accessToken == null)
                    throw new HttpRequestException("Nincs érvényes hozzáférési token.");

                request = new HttpRequestMessage(HttpMethod.Get, endpoint);
                request.Headers.Add("AccessToken", accessToken);
                request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            }
            else
            {
                request = new HttpRequestMessage(HttpMethod.Get, endpoint);
                request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            }

            var response = await _httpClient.SendAsync(request);
            return await HandleResponse<T>(response);
        }

        private async Task<T> HandleResponse<T>(HttpResponseMessage response)
        {
            if (response != null)
            {
                var jsonResponse = await response.Content.ReadAsStringAsync();
                if (response.IsSuccessStatusCode)
                {
                    return JsonConvert.DeserializeObject<T>(jsonResponse);
                }
                else
                {
                    throw new Exception($"Error fetching data: {response.StatusCode} - {jsonResponse}");
                }
            }
            throw new HttpRequestException("Üres válasz érkezett a szerverről");
        }



        // Hitelesítés nélküli egyszerű GET kérés
        public async Task<List<WeatherForecast>> GetWeatherForecastsAsync()
        {
            try
            {
                return await SendGetRequest<List<WeatherForecast>>("api/WeatherForecast/GetWeatherForecast", true);
            }
            catch (Exception ex)
            {
                // Log the exception (logging mechanism not shown here)
                throw new Exception("Hiba történt az időjárás előrejelzések lekérése során.", ex);
            }
        }
    }
}
