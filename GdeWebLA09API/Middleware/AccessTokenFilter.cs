using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace GdeWebLA09API.Middleware
{
    public class AccessTokenFilter : IActionFilter
    {
        private readonly ILogger<AccessTokenFilter> _logger;
        private readonly IConfiguration _configuration;

        public void OnActionExecuting(ActionExecutingContext context)
        {
            // Az action végrehajtása előtt végrehajtandó logika
            // Például: Ellenőrizhetjük a hozzáférési tokent itt
            if (context.HttpContext.Request.Headers.TryGetValue("AccessToken", out var accessTokenHeader))
            {
                if (accessTokenHeader == "gd3t0k3n")
                    return; // Érvényes token
            }
            
            // Érvénytelen token esetén visszautasítjuk a kérést
            context.Result = new ForbidResult("AccessToken érvénytelen vagy hiányzik.");
        }
        public void OnActionExecuted(ActionExecutedContext context)
        {
            // Az action végrehajtása után végrehajtandó logika
        }
    }
}
